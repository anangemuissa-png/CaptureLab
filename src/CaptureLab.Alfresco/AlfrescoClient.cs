using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CaptureLab.Alfresco;

public sealed class AlfrescoException(HttpStatusCode status, string message, string? body)
    : Exception($"Alfresco {(int)status} {status} : {message}")
{
    public HttpStatusCode Status { get; } = status;
    public string? Body { get; } = body;
}

/// <summary>Résumé d'un nœud Alfresco (fichier ou dossier).</summary>
public sealed record AlfrescoNode(string Id, string Name, string NodeType, string? Path, JsonObject? Properties);

/// <summary>
/// Client C# pour Alfresco Community (testé contre l'API REST v1 et CMIS 1.1 Browser binding).
/// Réécriture .NET moderne de la bibliothèque C# de 2023 (Millodeploie), sans dépendance externe.
/// </summary>
public sealed class AlfrescoClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly AlfrescoOptions _opt;
    private string? _ticket;

    private const string CoreApi = "alfresco/versions/1";
    private const string AuthApi = "authentication/versions/1";
    private const string SearchApi = "search/versions/1";
    private const string CmisBrowser = "cmis/versions/1.1/browser";

    public AlfrescoClient(AlfrescoOptions options, HttpMessageHandler? handler = null)
    {
        _opt = options;
        _http = new HttpClient(new RetryHandler(options.MaxRetries, handler ?? new HttpClientHandler()))
        {
            BaseAddress = new Uri(options.ApiRoot.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
        };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // ───────────────────────────── Authentification ─────────────────────────────

    /// <summary>
    /// Obtient un ticket (POST /tickets) puis l'utilise en Basic base64(ticket).
    /// Avantage vs Basic user:password à chaque appel : le mot de passe ne circule qu'une fois.
    /// </summary>
    public async Task LoginAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_opt.Username) || string.IsNullOrEmpty(_opt.Password))
            throw new InvalidOperationException("Identifiants absents : définir ALFRESCO_USERNAME et ALFRESCO_PASSWORD.");

        var body = new JsonObject { ["userId"] = _opt.Username, ["password"] = _opt.Password };
        using var response = await _http.PostAsync($"{AuthApi}/tickets", Json(body), ct);
        var json = await ReadJsonAsync(response, "authentification", ct);

        _ticket = json["entry"]!["id"]!.GetValue<string>();
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(_ticket)));
    }

    /// <summary>Invalide le ticket côté serveur (bonne pratique en fin de traitement).</summary>
    public async Task LogoutAsync(CancellationToken ct = default)
    {
        if (_ticket is null) return;
        using var _ = await _http.DeleteAsync($"{AuthApi}/tickets/-me-", ct);
        _ticket = null;
    }

    /// <summary>Test de connexion rapide : informations sur le dépôt (GET /discovery).</summary>
    public async Task<string> GetRepositoryVersionAsync(CancellationToken ct = default)
    {
        using var response = await _http.GetAsync("discovery", ct);
        var json = await ReadJsonAsync(response, "discovery", ct);
        var v = json["entry"]!["repository"]!["version"]!;
        return $"{json["entry"]!["repository"]!["edition"]} {v["display"]}";
    }

    // ───────────────────────────── Nœuds (REST v1) ─────────────────────────────

    /// <summary>GET /nodes/{id}?relativePath=… ; retourne null si 404.</summary>
    public async Task<AlfrescoNode?> GetNodeByPathAsync(string parentNodeId, string relativePath, CancellationToken ct = default)
    {
        var url = $"{CoreApi}/nodes/{parentNodeId}?relativePath={Uri.EscapeDataString(relativePath)}&include=path,properties";
        using var response = await _http.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        return ToNode((await ReadJsonAsync(response, "lecture nœud", ct))["entry"]!);
    }

    public async Task<AlfrescoNode> GetNodeAsync(string nodeId, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync($"{CoreApi}/nodes/{nodeId}?include=path,properties,aspectNames", ct);
        return ToNode((await ReadJsonAsync(response, "lecture nœud", ct))["entry"]!);
    }

    /// <summary>Crée un dossier (et ses parents grâce à relativePath). Idempotent : retourne l'existant si 409.</summary>
    public async Task<AlfrescoNode> EnsureFolderAsync(string parentNodeId, string relativePath, CancellationToken ct = default)
    {
        var existing = await GetNodeByPathAsync(parentNodeId, relativePath, ct);
        if (existing is not null) return existing;

        var parts = relativePath.Trim('/').Split('/');
        var body = new JsonObject
        {
            ["name"] = parts[^1],
            ["nodeType"] = "cm:folder",
            ["relativePath"] = string.Join('/', parts[..^1])
        };
        using var response = await _http.PostAsync($"{CoreApi}/nodes/{parentNodeId}/children", Json(body), ct);
        if (response.StatusCode == HttpStatusCode.Conflict) // créé entre-temps par un autre traitement
            return (await GetNodeByPathAsync(parentNodeId, relativePath, ct))!;
        return ToNode((await ReadJsonAsync(response, "création dossier", ct))["entry"]!);
    }

    /// <summary>
    /// Dépôt d'un fichier en multipart/form-data (POST /nodes/{parent}/children).
    /// - relativePath : crée automatiquement les dossiers intermédiaires ;
    /// - les propriétés (cm:title…) passent comme champs du formulaire ;
    /// - autoRename=false : un nom déjà pris renvoie 409 (on traite alors le doublon).
    /// </summary>
    public async Task<(AlfrescoNode Node, bool AlreadyExisted)> UploadAsync(
        string parentNodeId, string relativePath, string filePath, string targetName,
        IDictionary<string, string>? properties = null, CancellationToken ct = default)
    {
        var bytes = await File.ReadAllBytesAsync(filePath, ct); // ByteArrayContent : rejouable par le RetryHandler
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(GuessMime(filePath));
        form.Add(file, "filedata", targetName);
        form.Add(new StringContent(targetName), "name");
        form.Add(new StringContent("cm:content"), "nodeType");
        form.Add(new StringContent(relativePath), "relativePath");
        form.Add(new StringContent("false"), "autoRename");
        foreach (var (key, value) in properties ?? new Dictionary<string, string>())
            form.Add(new StringContent(value), key);

        using var response = await _http.PostAsync($"{CoreApi}/nodes/{parentNodeId}/children", form, ct);
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var existing = await GetNodeByPathAsync(parentNodeId, $"{relativePath}/{targetName}", ct)
                ?? throw new AlfrescoException(HttpStatusCode.Conflict, "nom en conflit mais nœud introuvable", null);
            return (existing, true);
        }
        return (ToNode((await ReadJsonAsync(response, "dépôt fichier", ct))["entry"]!), false);
    }

    /// <summary>
    /// Ajoute un aspect et ses propriétés (PUT /nodes/{id}).
    /// Attention : aspectNames REMPLACE la liste complète, il faut donc relire les aspects existants.
    /// </summary>
    public async Task AddAspectAsync(string nodeId, string aspect, IDictionary<string, object?> properties, CancellationToken ct = default)
    {
        using var get = await _http.GetAsync($"{CoreApi}/nodes/{nodeId}?include=aspectNames", ct);
        var current = (await ReadJsonAsync(get, "lecture aspects", ct))["entry"]!["aspectNames"]!.AsArray()
            .Select(a => a!.GetValue<string>()).ToList();
        if (!current.Contains(aspect)) current.Add(aspect);

        var props = new JsonObject();
        foreach (var (k, v) in properties) props[k] = v is null ? null : JsonValue.Create(v);

        var body = new JsonObject
        {
            ["aspectNames"] = new JsonArray(current.Select(a => (JsonNode)a).ToArray()),
            ["properties"] = props
        };
        using var put = await _http.PutAsync($"{CoreApi}/nodes/{nodeId}", Json(body), ct);
        await ReadJsonAsync(put, "ajout aspect", ct);
    }

    public async Task AddTagsAsync(string nodeId, IEnumerable<string> tags, CancellationToken ct = default)
    {
        var body = new JsonArray(tags.Where(t => !string.IsNullOrWhiteSpace(t))
                                     .Select(t => (JsonNode)new JsonObject { ["tag"] = t.ToLowerInvariant() }).ToArray());
        if (body.Count == 0) return;
        using var response = await _http.PostAsync($"{CoreApi}/nodes/{nodeId}/tags", Json(body), ct);
        await ReadJsonAsync(response, "étiquettes", ct);
    }

    /// <summary>
    /// Nouvelle version du contenu (PUT /nodes/{id}/content). Le nœud garde le même id,
    /// l'historique passe 1.0 → 1.1 (mineure) ou 2.0 (majeure). Visible avec GET /nodes/{id}/versions.
    /// </summary>
    public async Task<AlfrescoNode> UploadNewVersionAsync(string nodeId, string filePath, bool majorVersion, string comment, CancellationToken ct = default)
    {
        var content = new ByteArrayContent(await File.ReadAllBytesAsync(filePath, ct));
        content.Headers.ContentType = new MediaTypeHeaderValue(GuessMime(filePath));
        var url = $"{CoreApi}/nodes/{nodeId}/content?majorVersion={majorVersion.ToString().ToLowerInvariant()}&comment={Uri.EscapeDataString(comment)}";
        using var response = await _http.PutAsync(url, content, ct);
        return ToNode((await ReadJsonAsync(response, "nouvelle version", ct))["entry"]!);
    }

    /// <summary>Télécharge le contenu binaire (GET /nodes/{id}/content).</summary>
    public async Task DownloadAsync(string nodeId, string destinationPath, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync($"{CoreApi}/nodes/{nodeId}/content", ct);
        if (!response.IsSuccessStatusCode) await ReadJsonAsync(response, "téléchargement", ct);
        await using var file = File.Create(destinationPath);
        await response.Content.CopyToAsync(file, ct);
    }

    /// <summary>Suppression (va dans la corbeille d'Alfresco, sauf permanent=true).</summary>
    public async Task DeleteAsync(string nodeId, bool permanent = false, CancellationToken ct = default)
    {
        using var response = await _http.DeleteAsync($"{CoreApi}/nodes/{nodeId}?permanent={permanent.ToString().ToLowerInvariant()}", ct);
        if (!response.IsSuccessStatusCode) await ReadJsonAsync(response, "suppression", ct);
    }

    // ───────────────────────────── Recherche ─────────────────────────────

    /// <summary>
    /// Recherche AFTS (Alfresco Full Text Search) via l'API Search.
    /// Ex. : TYPE:"cm:content" AND cm:title:"FAC-2026*" AND TAG:"facture"
    /// Note : passe par l'index Solr/Search Services → délai d'indexation de quelques secondes.
    /// </summary>
    public async Task<IReadOnlyList<AlfrescoNode>> SearchAftsAsync(string query, int maxItems = 25, CancellationToken ct = default)
    {
        var body = new JsonObject
        {
            ["query"] = new JsonObject { ["query"] = query, ["language"] = "afts" },
            ["include"] = new JsonArray("properties", "path"),
            ["paging"] = new JsonObject { ["maxItems"] = maxItems, ["skipCount"] = 0 },
            ["sort"] = new JsonArray(new JsonObject { ["type"] = "FIELD", ["field"] = "cm:created", ["ascending"] = false })
        };
        using var response = await _http.PostAsync($"{SearchApi}/search", Json(body), ct);
        var json = await ReadJsonAsync(response, "recherche AFTS", ct);
        return json["list"]!["entries"]!.AsArray().Select(e => ToNode(e!["entry"]!)).ToList();
    }

    /// <summary>
    /// Requête CMIS-SQL via le Browser binding (JSON). Standard OASIS : le même code
    /// fonctionne sur d'autres GED compatibles CMIS.
    /// Ex. : SELECT cmis:objectId, cmis:name FROM cmis:document WHERE IN_FOLDER('…')
    /// </summary>
    public async Task<IReadOnlyList<JsonObject>> CmisQueryAsync(string cmisSql, int maxItems = 25, CancellationToken ct = default)
    {
        var url = $"{CmisBrowser}?cmisselector=query&succinct=true&maxItems={maxItems}&q={Uri.EscapeDataString(cmisSql)}";
        using var response = await _http.GetAsync(url, ct);
        var json = await ReadJsonAsync(response, "requête CMIS", ct);
        return json["results"]!.AsArray()
            .Select(r => r!["succinctProperties"]!.AsObject())
            .ToList();
    }

    // ───────────────────────────── Utilitaires ─────────────────────────────

    private static StringContent Json(JsonNode node) => new(node.ToJsonString(), Encoding.UTF8, "application/json");

    private static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response, string operation, CancellationToken ct)
    {
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            // Alfresco renvoie { "error": { "briefSummary": "...", "statusCode": ... } }
            string? summary = null;
            try { summary = JsonNode.Parse(text)?["error"]?["briefSummary"]?.GetValue<string>(); } catch (JsonException) { }
            throw new AlfrescoException(response.StatusCode, $"{operation} — {summary ?? "voir le corps de la réponse"}", text);
        }
        return string.IsNullOrWhiteSpace(text) ? new JsonObject() : JsonNode.Parse(text)!;
    }

    private static AlfrescoNode ToNode(JsonNode e) => new(
        e["id"]!.GetValue<string>(),
        e["name"]!.GetValue<string>(),
        e["nodeType"]?.GetValue<string>() ?? "",
        e["path"]?["name"]?.GetValue<string>(),
        e["properties"] as JsonObject);

    public static string GuessMime(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".tif" or ".tiff" => "image/tiff",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".txt" => "text/plain",
        _ => "application/octet-stream"
    };

    public void Dispose() => _http.Dispose();
}
