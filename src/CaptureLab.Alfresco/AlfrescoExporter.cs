using System.Text.RegularExpressions;
using CaptureLab.Core;

namespace CaptureLab.Alfresco;

/// <summary>
/// Export d'un document validé vers Alfresco :
///   Capture/{Type}/{Année}/{Mois}/{Type}_{Numéro}.ext
/// + cm:title, cm:description (empreinte SHA-256), étiquettes,
/// + aspect personnalisé facultatif (cap:numero, cap:dateDocument, …).
/// </summary>
public sealed partial class AlfrescoExporter : IDocumentExporter
{
    private readonly AlfrescoClient _client;
    private readonly AlfrescoOptions _opt;

    public AlfrescoExporter(AlfrescoClient client, AlfrescoOptions options)
    {
        _client = client;
        _opt = options;
    }

    public async Task<string> ExportAsync(CaptureDocument doc, CancellationToken ct = default)
    {
        var type = doc.Classification!.Type.ToString();
        var date = doc["date"] ?? DateTime.Today.ToString("yyyy-MM-dd");
        var folder = $"{_opt.RootFolder}/{type}/{date[..4]}/{date.Substring(5, 2)}";
        var name = Sanitize($"{type}_{doc["numero"]}") + Path.GetExtension(doc.SourcePath).ToLowerInvariant();

        var props = new Dictionary<string, string>
        {
            ["cm:title"] = $"{type} {doc["numero"]}",
            ["cm:description"] = $"Client : {doc["client"] ?? "n/d"} | Date : {date} | sha256:{doc.Sha256}"
        };

        var (node, existed) = await _client.UploadAsync(_opt.ParentNode, folder, doc.SourcePath, name, props, ct);
        if (existed)
        {
            doc.Trace($"Doublon : '{name}' existe déjà dans {folder} — pas de nouveau dépôt.");
            return node.Id;
        }

        await _client.AddTagsAsync(node.Id, [type, doc["client"] ?? ""], ct);

        if (!string.IsNullOrWhiteSpace(_opt.CustomAspect))
        {
            var p = _opt.CustomPrefix;
            await _client.AddAspectAsync(node.Id, _opt.CustomAspect, new Dictionary<string, object?>
            {
                [$"{p}:typeDocument"] = type,
                [$"{p}:numero"] = doc["numero"],
                [$"{p}:dateDocument"] = date,               // propriété d:date : format ISO
                [$"{p}:client"] = doc["client"],
                [$"{p}:montant"] = doc["montant"] is { } m ? decimal.Parse(m, System.Globalization.CultureInfo.InvariantCulture) : null,
                [$"{p}:sha256"] = doc.Sha256
            }, ct);
        }
        return node.Id;
    }

    /// <summary>Alfresco refuse * " &lt; &gt; \ / ? : | et un nom finissant par un point.</summary>
    public static string Sanitize(string name) => InvalidChars().Replace(name, "_").TrimEnd('.', ' ');

    [GeneratedRegex(@"[*""<>\\/?:|]")]
    private static partial Regex InvalidChars();
}
