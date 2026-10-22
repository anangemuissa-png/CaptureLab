using System.Globalization;
using System.Text;

namespace CaptureLab.Core;

/// <summary>
/// Classification par mots-clés pondérés. C'est volontairement simple : le but est de
/// pouvoir expliquer le principe (score par classe + seuil de confiance) qu'on retrouve,
/// en plus évolué (apprentissage sur échantillons), dans TotalAgility.
/// </summary>
public sealed class KeywordClassifier
{
    private readonly Dictionary<DocumentType, (string Keyword, int Weight)[]> _rules = new()
    {
        [DocumentType.Facture] =
        [
            ("facture", 5), ("invoice", 5), ("tps", 2), ("tvq", 2), ("sous-total", 2),
            ("total a payer", 3), ("montant du", 2), ("echeance", 1)
        ],
        [DocumentType.Connaissement] =
        [
            ("connaissement", 5), ("bill of lading", 5), ("expediteur", 2), ("shipper", 2),
            ("consignee", 2), ("destinataire", 1), ("port de chargement", 3), ("conteneur", 2), ("navire", 2)
        ],
        [DocumentType.BonLivraison] =
        [
            ("bon de livraison", 5), ("delivery note", 5), ("recu par", 3), ("signature", 1),
            ("quantite livree", 3), ("date de livraison", 2)
        ]
    };

    /// <summary>Confiance minimale pour accepter la classification automatiquement.</summary>
    public double Threshold { get; init; } = 0.6;

    public ClassificationResult Classify(string text)
    {
        var normalized = Normalize(text);
        var scores = new Dictionary<DocumentType, int>();

        foreach (var (docType, rules) in _rules)
            scores[docType] = rules.Where(r => normalized.Contains(r.Keyword)).Sum(r => r.Weight);

        var total = scores.Values.Sum();
        if (total == 0)
            return new ClassificationResult(DocumentType.Inconnu, 0, scores);

        var best = scores.MaxBy(kv => kv.Value);
        var confidence = Math.Round((double)best.Value / total, 2);

        // En dessous du seuil : on ne devine pas, on envoie en validation manuelle.
        var type = confidence >= Threshold ? best.Key : DocumentType.Inconnu;
        return new ClassificationResult(type, confidence, scores);
    }

    /// <summary>Minuscules + suppression des accents : "Échéance" → "echeance". Rend l'OCR plus tolérant.</summary>
    public static string Normalize(string text)
    {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
