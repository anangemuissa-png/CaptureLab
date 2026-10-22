namespace CaptureLab.Core;

/// <summary>Types de documents reconnus par la chaîne (contexte : transport / logistique).</summary>
public enum DocumentType
{
    Inconnu,
    Facture,
    Connaissement,   // Bill of Lading (BL)
    BonLivraison
}

/// <summary>Statut d'un lot / document dans la chaîne, comme dans un outil IDP.</summary>
public enum CaptureStatus
{
    Recu,
    OcrTermine,
    Classe,
    Extrait,
    Valide,
    EnException,     // validation manuelle requise (équivalent d'une file "Validation" dans TotalAgility)
    Depose,          // archivé dans la GED
    Erreur
}

/// <summary>Un champ extrait, avec sa confiance (comme un champ d'extraction IDP).</summary>
public sealed record ExtractedField(string Name, string? Value, double Confidence);

/// <summary>Résultat de classification : type retenu + score.</summary>
public sealed record ClassificationResult(DocumentType Type, double Confidence, IReadOnlyDictionary<DocumentType, int> Scores);

/// <summary>Résultat de validation métier.</summary>
public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors);

/// <summary>Document qui traverse la chaîne : Capture → OCR → Classification → Extraction → Validation → Export.</summary>
public sealed class CaptureDocument
{
    public required string SourcePath { get; init; }
    public string FileName => Path.GetFileName(SourcePath);
    public string Sha256 { get; set; } = "";
    public string OcrText { get; set; } = "";
    public ClassificationResult? Classification { get; set; }
    public Dictionary<string, ExtractedField> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ValidationResult? Validation { get; set; }
    public CaptureStatus Status { get; set; } = CaptureStatus.Recu;
    public string? AlfrescoNodeId { get; set; }
    public List<string> Log { get; } = new();

    public string? this[string field] => Fields.TryGetValue(field, out var f) ? f.Value : null;

    public void Trace(string message) => Log.Add($"{DateTime.Now:HH:mm:ss} {message}");
}
