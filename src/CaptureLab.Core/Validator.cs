using System.Globalization;

namespace CaptureLab.Core;

/// <summary>
/// Règles de validation métier. Tout document qui échoue part en "exception"
/// (file de validation humaine) au lieu d'être archivé avec des données fausses.
/// </summary>
public sealed class BusinessValidator
{
    public double MinClassificationConfidence { get; init; } = 0.6;

    public ValidationResult Validate(CaptureDocument doc)
    {
        var errors = new List<string>();
        var type = doc.Classification?.Type ?? DocumentType.Inconnu;

        if (type == DocumentType.Inconnu)
            errors.Add("Type de document non reconnu (classification sous le seuil).");
        else if (doc.Classification!.Confidence < MinClassificationConfidence)
            errors.Add($"Confiance de classification trop basse ({doc.Classification.Confidence:P0}).");

        Require(doc, "numero", errors);
        Require(doc, "date", errors);

        if (doc["date"] is { } date)
        {
            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                errors.Add($"Date invalide : {date}.");
            else if (d > DateOnly.FromDateTime(DateTime.Today.AddDays(1)))
                errors.Add($"Date dans le futur : {date}.");
        }

        if (type == DocumentType.Facture)
        {
            Require(doc, "montant", errors);
            if (doc["montant"] is { } m &&
                decimal.TryParse(m, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount <= 0)
                errors.Add($"Montant non positif : {m}.");
        }

        if (type == DocumentType.Connaissement && doc["conteneur"] is null)
            errors.Add("Numéro de conteneur (ISO 6346) manquant sur le connaissement.");

        return new ValidationResult(errors.Count == 0, errors);
    }

    private static void Require(CaptureDocument doc, string field, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(doc[field]))
            errors.Add($"Champ obligatoire manquant : {field}.");
    }
}
