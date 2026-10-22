using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Web;

namespace CaptureLab.SelfTest;

/// <summary>
/// Faux serveur Alfresco en mémoire : reproduit les réponses de l'API REST v1 utilisées
/// par AlfrescoClient (tickets, nœuds, multipart, 409, aspects, étiquettes, recherche, CMIS).
/// Permet de tester le client sans Docker ni réseau.
/// </summary>
public sealed class FakeAlfrescoHandler : HttpMessageHandler
{
    public sealed class Node
    {
        public required string Id, Name, Type, ParentId;
        public List<string> Aspects = ["cm:auditable"];
        public JsonObject Props = new();
        public List<string> Tags = new();
        public byte[] Content = [];
    }

    public const string Ticket = "TICKET_fake123";
    public Dictionary<string, Node> Nodes { get; } = new();
    public List<string> Calls { get; } = new();
    public int FailNextWith503 { get; set; }
    private int _seq;

    public FakeAlfrescoHandler()
    {
        Nodes["shared-root"] = new Node { Id = "shared-root", Name = "Shared", Type = "cm:folder", ParentId = "" };
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        var path = req.RequestUri!.AbsolutePath;
        var query = HttpUtility.ParseQueryString(req.RequestUri.Query);
        Calls.Add($"{req.Method} {path}");

        if (FailNextWith503 > 0) { FailNextWith503--; return Error(HttpStatusCode.ServiceUnavailable, "démarrage"); }

        // Authentification
        if (path.EndsWith("/authentication/versions/1/tickets") && req.Method == HttpMethod.Post)
        {
            var body = JsonNode.Parse(await req.Content!.ReadAsStringAsync(ct))!;
            return body["password"]?.GetValue<string>() == "secret-de-test"
                ? Ok(new JsonObject { ["entry"] = new JsonObject { ["id"] = Ticket, ["userId"] = body["userId"]!.GetValue<string>() } }, HttpStatusCode.Created)
                : Error(HttpStatusCode.Forbidden, "Login failed");
        }

        // Toute autre requête exige le ticket en Basic base64(ticket)
        var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes(Ticket));
        if (req.Headers.Authorization?.Parameter != expected)
            return Error(HttpStatusCode.Unauthorized, "Authentication required");

        if (path.EndsWith("/tickets/-me-")) return new HttpResponseMessage(HttpStatusCode.NoContent);

        if (path.EndsWith("/discovery"))
            return Ok(JsonNode.Parse("""{"entry":{"repository":{"edition":"Community","version":{"display":"26.1.0 (fake)"}}}}""")!);

        if (path.EndsWith("/search/versions/1/search"))
        {
            var q = JsonNode.Parse(await req.Content!.ReadAsStringAsync(ct))!["query"]!["query"]!.GetValue<string>();
            var hits = Nodes.Values.Where(n => n.Type == "cm:content" &&
                       (q.Contains("TAG:") ? n.Tags.Any(t => q.Contains(t)) : true)).ToList();
            return Ok(new JsonObject { ["list"] = new JsonObject { ["entries"] = new JsonArray(hits.Select(h => (JsonNode)new JsonObject { ["entry"] = Entry(h) }).ToArray()) } });
        }

        if (path.Contains("/cmis/versions/1.1/browser"))
        {
            var rows = Nodes.Values.Where(n => n.Type == "cm:content")
                .Select(n => (JsonNode)new JsonObject { ["succinctProperties"] = new JsonObject { ["cmis:objectId"] = n.Id, ["cmis:name"] = n.Name } });
            return Ok(new JsonObject { ["results"] = new JsonArray(rows.ToArray()), ["numItems"] = Nodes.Count });
        }

        // /nodes/{id}[/children|/tags]
        var marker = "/nodes/";
        var idx = path.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return Error(HttpStatusCode.NotFound, "route inconnue");
        var rest = path[(idx + marker.Length)..].Split('/');
        var id = Resolve(rest[0]);
        if (id is null || !Nodes.ContainsKey(id)) return Error(HttpStatusCode.NotFound, "nœud inconnu");

        if (rest.Length == 1 && req.Method == HttpMethod.Get)
        {
            var target = query["relativePath"] is { } rp ? Walk(id, rp) : Nodes[id];
            return target is null ? Error(HttpStatusCode.NotFound, "chemin inconnu") : Ok(new JsonObject { ["entry"] = Entry(target) });
        }

        if (rest.Length == 1 && req.Method == HttpMethod.Put)
        {
            var body = JsonNode.Parse(await req.Content!.ReadAsStringAsync(ct))!;
            var n = Nodes[id];
            if (body["aspectNames"] is JsonArray a) n.Aspects = a.Select(x => x!.GetValue<string>()).ToList();
            if (body["properties"] is JsonObject p) foreach (var kv in p) n.Props[kv.Key] = kv.Value?.DeepClone();
            return Ok(new JsonObject { ["entry"] = Entry(n) });
        }

        if (rest.Length == 2 && rest[1] == "tags" && req.Method == HttpMethod.Post)
        {
            var tags = JsonNode.Parse(await req.Content!.ReadAsStringAsync(ct))!.AsArray();
            Nodes[id].Tags.AddRange(tags.Select(t => t!["tag"]!.GetValue<string>()));
            return Ok(new JsonObject(), HttpStatusCode.Created);
        }

        if (rest.Length == 2 && rest[1] == "children" && req.Method == HttpMethod.Post)
        {
            string name, type, relativePath = "";
            var props = new JsonObject();
            byte[] content = [];
            if (req.Content is MultipartFormDataContent form)
            {
                type = "cm:content"; name = "";
                foreach (var part in form)
                {
                    var field = part.Headers.ContentDisposition!.Name!.Trim('"');
                    if (field == "filedata") { content = await part.ReadAsByteArrayAsync(ct); continue; }
                    var value = await part.ReadAsStringAsync(ct);
                    switch (field)
                    {
                        case "name": name = value; break;
                        case "nodeType": type = value; break;
                        case "relativePath": relativePath = value; break;
                        case "autoRename": break;
                        default: props[field] = value; break;
                    }
                }
            }
            else
            {
                var body = JsonNode.Parse(await req.Content!.ReadAsStringAsync(ct))!;
                name = body["name"]!.GetValue<string>();
                type = body["nodeType"]!.GetValue<string>();
                relativePath = body["relativePath"]?.GetValue<string>() ?? "";
            }

            var parent = MkDirs(id, relativePath);
            if (Nodes.Values.Any(n => n.ParentId == parent.Id && n.Name == name))
                return Error(HttpStatusCode.Conflict, $"Duplicate child name not allowed: {name}");

            var node = new Node { Id = $"node-{++_seq}", Name = name, Type = type, ParentId = parent.Id, Props = props, Content = content };
            if (props.ContainsKey("cm:title")) node.Aspects.Add("cm:titled");
            Nodes[node.Id] = node;
            return Ok(new JsonObject { ["entry"] = Entry(node) }, HttpStatusCode.Created);
        }

        return Error(HttpStatusCode.MethodNotAllowed, "non simulé");
    }

    private static string? Resolve(string alias) => alias switch { "-shared-" => "shared-root", _ => alias };

    private Node? Walk(string fromId, string relativePath)
    {
        var current = Nodes[fromId];
        foreach (var part in relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = Nodes.Values.FirstOrDefault(n => n.ParentId == current.Id && n.Name == part);
            if (current is null) return null;
        }
        return current;
    }

    private Node MkDirs(string fromId, string relativePath)
    {
        var current = Nodes[fromId];
        foreach (var part in relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var next = Nodes.Values.FirstOrDefault(n => n.ParentId == current.Id && n.Name == part);
            if (next is null)
            {
                next = new Node { Id = $"node-{++_seq}", Name = part, Type = "cm:folder", ParentId = current.Id };
                Nodes[next.Id] = next;
            }
            current = next;
        }
        return current;
    }

    public string PathOf(Node n)
    {
        var parts = new List<string>();
        for (var p = Nodes.GetValueOrDefault(n.ParentId); p is not null; p = Nodes.GetValueOrDefault(p.ParentId))
            parts.Insert(0, p.Name);
        return "/" + string.Join('/', parts);
    }

    private JsonObject Entry(Node n) => new()
    {
        ["id"] = n.Id, ["name"] = n.Name, ["nodeType"] = n.Type,
        ["isFile"] = n.Type == "cm:content", ["isFolder"] = n.Type == "cm:folder",
        ["aspectNames"] = new JsonArray(n.Aspects.Select(a => (JsonNode)a).ToArray()),
        ["properties"] = n.Props.DeepClone(),
        ["path"] = new JsonObject { ["name"] = PathOf(n) }
    };

    private static HttpResponseMessage Ok(JsonNode body, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Error(HttpStatusCode code, string summary) =>
        Ok(new JsonObject { ["error"] = new JsonObject { ["briefSummary"] = summary, ["statusCode"] = (int)code } }, code);
}
