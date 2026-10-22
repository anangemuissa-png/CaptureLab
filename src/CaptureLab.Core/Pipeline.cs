using System.Security.Cryptography;

namespace CaptureLab.Core;

/// <summary>Destination finale d'un document validé (Alfresco, OnBase, SharePoint, dossier…).</summary>
public interface IDocumentExporter
{
    /// <returns>Identifiant du document dans le système cible.</returns>
    Task<string> ExportAsync(CaptureDocument doc, CancellationToken ct = default);
}

/// <summary>
/// Orchestration de la chaîne complète. Chaque étape met à jour le statut et la trace,
/// ce qui permet le soutien de niveau 2/3 : on sait exactement où un document s'est arrêté.
/// </summary>
public sealed class CapturePipeline
{
    private readonly IOcrEngine _ocr;
    private readonly KeywordClassifier _classifier;
    private readonly RegexExtractor _extractor;
    private readonly BusinessValidator _validator;
    private readonly IDocumentExporter? _exporter;

    public CapturePipeline(IOcrEngine ocr, KeywordClassifier classifier, RegexExtractor extractor,
                           BusinessValidator validator, IDocumentExporter? exporter)
    {
        _ocr = ocr;
        _classifier = classifier;
        _extractor = extractor;
        _validator = validator;
        _exporter = exporter;
    }

    public async Task<CaptureDocument> ProcessAsync(string path, CancellationToken ct = default)
    {
        var doc = new CaptureDocument { SourcePath = path };
        try
        {
            doc.Sha256 = await HashAsync(path, ct);
            doc.Trace($"Reçu ({doc.Sha256[..12]}…)");

            doc.OcrText = await _ocr.ReadTextAsync(path, ct);
            doc.Status = CaptureStatus.OcrTermine;
            doc.Trace($"OCR : {doc.OcrText.Length} caractères");

            doc.Classification = _classifier.Classify(doc.OcrText);
            doc.Status = CaptureStatus.Classe;
            doc.Trace($"Classé : {doc.Classification.Type} ({doc.Classification.Confidence:P0})");

            _extractor.Extract(doc);
            doc.Status = CaptureStatus.Extrait;
            doc.Trace("Extrait : " + string.Join(", ", doc.Fields.Values.Select(f => $"{f.Name}={f.Value ?? "∅"}")));

            doc.Validation = _validator.Validate(doc);
            if (!doc.Validation.IsValid)
            {
                doc.Status = CaptureStatus.EnException;
                doc.Trace("Exception : " + string.Join(" | ", doc.Validation.Errors));
                return doc;
            }
            doc.Status = CaptureStatus.Valide;

            if (_exporter is null)
            {
                doc.Trace("Mode hors ligne : pas de dépôt GED.");
                return doc;
            }

            doc.AlfrescoNodeId = await _exporter.ExportAsync(doc, ct);
            doc.Status = CaptureStatus.Depose;
            doc.Trace($"Déposé dans la GED : nœud {doc.AlfrescoNodeId}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            doc.Status = CaptureStatus.Erreur;
            doc.Trace($"ERREUR {ex.GetType().Name} : {ex.Message}");
        }
        return doc;
    }

    /// <summary>Empreinte SHA-256 : sert à détecter les doublons et à prouver l'intégrité du fichier.</summary>
    public static async Task<string> HashAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
