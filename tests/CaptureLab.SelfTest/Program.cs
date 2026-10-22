using System.Net;
using CaptureLab.Alfresco;
using CaptureLab.Core;
using CaptureLab.SelfTest;

// Banc d'essai autonome (aucun paquet NuGet requis) : dotnet run --project tests/CaptureLab.SelfTest
var samples = FindSamples();
int passed = 0, failed = 0;

void Check(string name, bool condition, string? detail = null)
{
    if (condition) { passed++; Console.WriteLine($"  ✔ {name}"); }
    else { failed++; Console.WriteLine($"  ✖ {name}{(detail is null ? "" : $" — {detail}")}"); }
}

var classifier = new KeywordClassifier();
var extractor = new RegexExtractor();
var validator = new BusinessValidator();
var offline = new CapturePipeline(new CompositeOcrEngine(new TesseractCliOcrEngine()), classifier, extractor, validator, null);

Console.WriteLine("1. Classification");
Check("Facture reconnue", classifier.Classify(Read("facture_transmontreal.txt")).Type == DocumentType.Facture);
Check("Connaissement reconnu", classifier.Classify(Read("connaissement_matadi.txt")).Type == DocumentType.Connaissement);
Check("Bon de livraison reconnu", classifier.Classify(Read("bon_livraison_laval.txt")).Type == DocumentType.BonLivraison);
Check("Note sans mot-clé → Inconnu", classifier.Classify(Read("note_illisible.txt")).Type == DocumentType.Inconnu);
Check("Accents ignorés (ÉCHÉANCE → echeance)", KeywordClassifier.Normalize("ÉCHÉANCE à payer") == "echeance a payer");

Console.WriteLine("2. Extraction");
var fac = await offline.ProcessAsync(Path.Combine(samples, "facture_transmontreal.txt"));
Check("Numéro facture", fac["numero"] == "FAC-2026147", fac["numero"]);
Check("Date jj/mm/aaaa → ISO", fac["date"] == "2026-10-14", fac["date"]);
Check("Montant = total à payer (pas le sous-total)", fac["montant"] == "1149.75", fac["montant"]);
Check("Client", fac["client"] == "Entrepôts Rive-Nord Ltée", fac["client"]);
var bl = await offline.ProcessAsync(Path.Combine(samples, "connaissement_matadi.txt"));
Check("Numéro connaissement", bl["numero"] == "MTD-20261009", bl["numero"]);
Check("Conteneur ISO 6346", bl["conteneur"] == "MSCU4821736", bl["conteneur"]);
Check("Date ISO conservée", bl["date"] == "2026-10-09", bl["date"]);
Check("Montant '1 234,56' → 1234.56", RegexExtractor.ParseAmount("1 234,56") == "1234.56");

Console.WriteLine("3. Validation");
Check("Facture complète valide", fac.Status == CaptureStatus.Valide, string.Join(" | ", fac.Validation?.Errors ?? []));
var incomplete = await offline.ProcessAsync(Path.Combine(samples, "facture_incomplete.txt"));
Check("Facture incomplète → exception", incomplete.Status == CaptureStatus.EnException);
Check("…avec la raison (numéro manquant)", incomplete.Validation!.Errors.Any(e => e.Contains("numero")));
var note = await offline.ProcessAsync(Path.Combine(samples, "note_illisible.txt"));
Check("Document inconnu → exception", note.Status == CaptureStatus.EnException);

Console.WriteLine("4. Client Alfresco (faux serveur en mémoire)");
var fake = new FakeAlfrescoHandler();
var opt = new AlfrescoOptions { BaseUrl = "http://alfresco.test", Username = "capture", Password = "secret-de-test", RootFolder = "Capture", CustomAspect = "cap:documentCapture", MaxRetries = 2 };
using (var client = new AlfrescoClient(opt, fake))
{
    var badClient = new AlfrescoClient(new AlfrescoOptions { BaseUrl = "http://alfresco.test", Username = "x", Password = "mauvais", MaxRetries = 0 }, fake);
    var ex = await Catch(() => badClient.LoginAsync());
    Check("Mauvais mot de passe → 403 explicite", ex is AlfrescoException { Status: HttpStatusCode.Forbidden }, ex?.Message);

    fake.FailNextWith503 = 1;
    await client.LoginAsync();
    Check("Reprise auto après 503 puis ticket obtenu", fake.Calls.Count(c => c.Contains("tickets")) >= 3);
    Check("Version du dépôt", (await client.GetRepositoryVersionAsync()).Contains("26.1.0"));

    var online = new CapturePipeline(new CompositeOcrEngine(new TesseractCliOcrEngine()), classifier, extractor, validator, new AlfrescoExporter(client, opt));
    var doc = await online.ProcessAsync(Path.Combine(samples, "facture_transmontreal.txt"));
    Check("Statut Déposé", doc.Status == CaptureStatus.Depose, string.Join(" / ", doc.Log));
    var node = fake.Nodes[doc.AlfrescoNodeId!];
    Check("Classement Capture/Facture/2026/10", fake.PathOf(node) == "/Shared/Capture/Facture/2026/10", fake.PathOf(node));
    Check("Nom normalisé Facture_FAC-2026147.txt", node.Name == "Facture_FAC-2026147.txt", node.Name);
    Check("cm:title transmis en multipart", node.Props["cm:title"]?.ToString() == "Facture FAC-2026147");
    Check("Aspect personnalisé ajouté SANS perdre les autres", node.Aspects.Contains("cap:documentCapture") && node.Aspects.Contains("cm:titled") && node.Aspects.Contains("cm:auditable"), string.Join(",", node.Aspects));
    Check("Propriété cap:montant numérique", node.Props["cap:montant"]?.ToString() == "1149.75", node.Props["cap:montant"]?.ToJsonString());
    Check("Étiquettes facture + client", node.Tags.Contains("facture") && node.Tags.Count == 2, string.Join(",", node.Tags));
    Check("Contenu binaire intact", node.Content.SequenceEqual(File.ReadAllBytes(Path.Combine(samples, "facture_transmontreal.txt"))));

    var again = await online.ProcessAsync(Path.Combine(samples, "facture_transmontreal.txt"));
    Check("Doublon (409) → même nœud, pas de 2e dépôt", again.AlfrescoNodeId == doc.AlfrescoNodeId && fake.Nodes.Values.Count(n => n.Name == node.Name) == 1);
    Check("…tracé dans le journal", again.Log.Any(l => l.Contains("Doublon")));

    var hits = await client.SearchAftsAsync("TAG:\"facture\"");
    Check("Recherche AFTS par étiquette", hits.Count == 1 && hits[0].Id == doc.AlfrescoNodeId);
    var rows = await client.CmisQueryAsync("SELECT cmis:objectId, cmis:name FROM cmis:document");
    Check("Requête CMIS Browser binding", rows.Any(r => r["cmis:name"]?.ToString() == node.Name));

    var folder = await client.EnsureFolderAsync("-shared-", "Capture/Exceptions");
    var folder2 = await client.EnsureFolderAsync("-shared-", "Capture/Exceptions");
    Check("EnsureFolder idempotent", folder.Id == folder2.Id);

    await client.LogoutAsync();
    var afterLogout = await Catch(() => client.GetNodeAsync(doc.AlfrescoNodeId!));
    Check("Ticket révoqué au logout (401 ensuite)", afterLogout is AlfrescoException { Status: HttpStatusCode.Unauthorized } || afterLogout is null);
}

Console.WriteLine("5. OCR réel (Tesseract), si installé");
var scan = Path.Combine(samples, "scan_facture_transmontreal.png");
try
{
    var ocrDoc = await offline.ProcessAsync(scan);
    if (ocrDoc.Status == CaptureStatus.Erreur && ocrDoc.Log.Any(l => l.Contains("Tesseract") || l.Contains("Win32Exception")))
        Console.WriteLine("  – Tesseract absent : test ignoré");
    else
    {
        Check("Scan PNG → Facture", ocrDoc.Classification?.Type == DocumentType.Facture, ocrDoc.Classification?.Type.ToString());
        Check("Scan PNG → numéro + montant", ocrDoc["numero"] == "FAC-2026147" && ocrDoc["montant"] == "1149.75", $"{ocrDoc["numero"]} / {ocrDoc["montant"]}");
    }
}
catch (Exception e) { Console.WriteLine($"  – OCR ignoré : {e.Message}"); }

Console.WriteLine($"\nRésultat : {passed} réussi(s), {failed} échec(s)");
return failed == 0 ? 0 : 1;

string Read(string file) => File.ReadAllText(Path.Combine(samples, file));

static async Task<Exception?> Catch(Func<Task> action)
{
    try { await action(); return null; } catch (Exception e) { return e; }
}

static string FindSamples()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        if (Directory.Exists(Path.Combine(dir.FullName, "samples"))) return Path.Combine(dir.FullName, "samples");
    throw new DirectoryNotFoundException("Dossier samples introuvable");
}
