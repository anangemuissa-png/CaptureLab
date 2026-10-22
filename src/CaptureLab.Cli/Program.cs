using System.Text;
using CaptureLab.Alfresco;
using CaptureLab.Cli;
using CaptureLab.Core;

Console.OutputEncoding = Encoding.UTF8;
var cfg = AppConfig.Load();
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

if (args.Length == 0) return Usage();

try
{
    return args[0].ToLowerInvariant() switch
    {
        "ping" => await Ping(),
        "run" when args.Length >= 2 => await Run(args[1], offline: args.Contains("--offline"), noMove: args.Contains("--no-move")),
        "watch" when args.Length >= 2 => await Watch(args[1]),
        "search" when args.Length >= 2 => await Search(args[1]),
        "cmis" when args.Length >= 2 => await Cmis(args[1]),
        _ => Usage()
    };
}
catch (AlfrescoException ex)
{
    Console.Error.WriteLine($"✖ {ex.Message}");
    return 2;
}
catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
{
    Console.Error.WriteLine($"✖ {ex.Message}");
    return 2;
}

// ─────────────────────────────────────────────────────────────

async Task<int> Ping()
{
    using var client = new AlfrescoClient(cfg.Alfresco);
    Console.WriteLine($"Environnement : {cfg.Environment}");
    await client.LoginAsync(cts.Token);
    Console.WriteLine("✔ Authentification par ticket réussie");
    Console.WriteLine($"✔ Dépôt : {await client.GetRepositoryVersionAsync(cts.Token)}");
    var root = await client.EnsureFolderAsync(cfg.Alfresco.ParentNode, cfg.Alfresco.RootFolder, cts.Token);
    Console.WriteLine($"✔ Dossier de capture : {root.Path}/{root.Name} ({root.Id})");
    await client.LogoutAsync(cts.Token);
    return 0;
}

async Task<int> Run(string inputDir, bool offline, bool noMove)
{
    if (!Directory.Exists(inputDir)) { Console.Error.WriteLine($"Dossier introuvable : {inputDir}"); return 1; }

    var files = Directory.EnumerateFiles(inputDir)
        .Where(f => CompositeOcrEngine.SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
        .OrderBy(f => f).ToList();
    Console.WriteLine($"Environnement : {cfg.Environment} | {files.Count} fichier(s) | mode {(offline ? "HORS LIGNE" : "Alfresco")}");
    if (files.Count == 0) return 0;

    AlfrescoClient? client = null;
    if (!offline)
    {
        client = new AlfrescoClient(cfg.Alfresco);
        await client.LoginAsync(cts.Token);
    }

    var pipeline = BuildPipeline(client);
    var results = new List<CaptureDocument>();
    foreach (var file in files)
    {
        var doc = await pipeline.ProcessAsync(file, cts.Token);
        results.Add(doc);
        PrintDoc(doc);
        if (cfg.Capture.MoveFiles && !noMove && !offline) MoveToOutcomeFolder(inputDir, doc);
    }

    if (client is not null) { await client.LogoutAsync(cts.Token); client.Dispose(); }

    var report = WriteReport(inputDir, results);
    Console.WriteLine();
    Console.WriteLine("Résumé : " + string.Join(" | ", results.GroupBy(r => r.Status).Select(g => $"{g.Key}={g.Count()}")));
    Console.WriteLine($"Rapport : {report}");
    return results.Any(r => r.Status == CaptureStatus.Erreur) ? 3 : 0;
}

async Task<int> Watch(string inputDir)
{
    // "Dossier surveillé" (hot folder) : chaque fichier déposé est traité automatiquement.
    Directory.CreateDirectory(inputDir);
    using var client = new AlfrescoClient(cfg.Alfresco);
    await client.LoginAsync(cts.Token);
    var pipeline = BuildPipeline(client);
    using var watcher = new FileSystemWatcher(inputDir) { EnableRaisingEvents = true };
    Console.WriteLine($"Surveillance de {Path.GetFullPath(inputDir)} — Ctrl+C pour arrêter.");

    var queue = System.Threading.Channels.Channel.CreateUnbounded<string>();
    watcher.Created += (_, e) => queue.Writer.TryWrite(e.FullPath);

    try
    {
        await foreach (var path in queue.Reader.ReadAllAsync(cts.Token))
        {
            if (!CompositeOcrEngine.SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant())) continue;
            await WaitUntilReadable(path, cts.Token); // le scanner peut encore être en train d'écrire
            var doc = await pipeline.ProcessAsync(path, cts.Token);
            PrintDoc(doc);
            MoveToOutcomeFolder(inputDir, doc);
        }
    }
    catch (OperationCanceledException) { }
    await client.LogoutAsync();
    return 0;
}

async Task<int> Search(string afts)
{
    using var client = new AlfrescoClient(cfg.Alfresco);
    await client.LoginAsync(cts.Token);
    var nodes = await client.SearchAftsAsync(afts, ct: cts.Token);
    Console.WriteLine($"{nodes.Count} résultat(s) pour : {afts}");
    foreach (var n in nodes)
        Console.WriteLine($"  {n.Name,-40} {n.Path}  [{n.Id}]  {n.Properties?["cm:title"]}");
    await client.LogoutAsync(cts.Token);
    return 0;
}

async Task<int> Cmis(string sql)
{
    using var client = new AlfrescoClient(cfg.Alfresco);
    await client.LoginAsync(cts.Token);
    var rows = await client.CmisQueryAsync(sql, ct: cts.Token);
    Console.WriteLine($"{rows.Count} ligne(s) pour : {sql}");
    foreach (var row in rows)
        Console.WriteLine("  " + string.Join(" | ", row.Select(kv => $"{kv.Key}={kv.Value}")));
    await client.LogoutAsync(cts.Token);
    return 0;
}

CapturePipeline BuildPipeline(AlfrescoClient? client) => new(
    new CompositeOcrEngine(new TesseractCliOcrEngine(cfg.Ocr.TesseractPath, cfg.Ocr.Languages)),
    new KeywordClassifier { Threshold = cfg.Capture.ClassificationThreshold },
    new RegexExtractor(),
    new BusinessValidator { MinClassificationConfidence = cfg.Capture.ClassificationThreshold },
    client is null ? null : new AlfrescoExporter(client, cfg.Alfresco));

static void PrintDoc(CaptureDocument doc)
{
    var icon = doc.Status switch
    {
        CaptureStatus.Depose or CaptureStatus.Valide => "✔",
        CaptureStatus.EnException => "⚠",
        _ => "✖"
    };
    Console.WriteLine($"\n{icon} {doc.FileName} → {doc.Status}");
    foreach (var line in doc.Log) Console.WriteLine($"    {line}");
}

static void MoveToOutcomeFolder(string inputDir, CaptureDocument doc)
{
    var sub = doc.Status switch
    {
        CaptureStatus.Depose => "_traites",
        CaptureStatus.EnException => "_exceptions",
        _ => "_erreurs"
    };
    var dir = Directory.CreateDirectory(Path.Combine(inputDir, sub)).FullName;
    var dest = Path.Combine(dir, doc.FileName);
    if (File.Exists(dest)) dest = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(doc.FileName)}_{DateTime.Now:HHmmss}{Path.GetExtension(doc.FileName)}");
    File.Move(doc.SourcePath, dest);
}

static string WriteReport(string inputDir, List<CaptureDocument> results)
{
    var dir = Directory.CreateDirectory(Path.Combine(inputDir, "_rapports")).FullName;
    var path = Path.Combine(dir, $"rapport_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
    var sb = new StringBuilder("fichier;statut;type;confiance;numero;date;client;montant;conteneur;noeud_alfresco;erreurs\n");
    foreach (var d in results)
    {
        string C(string? v) => (v ?? "").Replace(';', ',');
        sb.Append(string.Join(';',
            C(d.FileName), d.Status, d.Classification?.Type, d.Classification?.Confidence.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            C(d["numero"]), C(d["date"]), C(d["client"]), C(d["montant"]), C(d["conteneur"]),
            C(d.AlfrescoNodeId), C(string.Join(" / ", d.Validation?.Errors ?? []))));
        sb.Append('\n');
    }
    File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true)); // BOM : ouverture correcte dans Excel
    return path;
}

static async Task WaitUntilReadable(string path, CancellationToken ct)
{
    for (var i = 0; i < 20; i++)
    {
        try { await using var _ = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None); return; }
        catch (IOException) { await Task.Delay(500, ct); }
    }
}

static int Usage()
{
    Console.WriteLine("""
        CaptureLab — chaîne de capture documentaire C# → Alfresco

        Commandes :
          ping                      Teste la connexion (ticket + version du dépôt + dossier Capture)
          run <dossier> [--offline] [--no-move]
                                    Traite tous les fichiers (OCR → classification → extraction → validation → dépôt)
                                    --offline : sans Alfresco (pour tester l'OCR et l'extraction)
          watch <dossier>           Dossier surveillé : traite chaque fichier déposé
          search "<requête AFTS>"   Ex. : search "TAG:facture AND cm:title:\"Facture*\""
          cmis "<requête CMIS-SQL>" Ex. : cmis "SELECT cmis:name FROM cmis:document WHERE cmis:name LIKE 'Facture%'"

        Variables d'environnement : ALFRESCO_USERNAME, ALFRESCO_PASSWORD, ALFRESCO_BASEURL (facultatif),
                                    CAPTURELAB_ENVIRONMENT = Development (test) | Production
        """);
    return 1;
}
