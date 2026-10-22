using System.Globalization;
using System.Text.RegularExpressions;

namespace CaptureLab.Core;

/// <summary>
/// Extraction de champs par expressions régulières, selon le type de document.
/// Équivalent "maison" des localisateurs d'extraction (format locators) d'un outil IDP.
/// </summary>
public sealed class RegexExtractor
{
    private static readonly RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // Numéros : on tolère les confusions OCR classiques (O/0) grâce à la normalisation plus bas.
    private static readonly Regex NumFacture = new(@"(?:facture|invoice)\s*(?:n[o°º]?|#|no\.?)?\s*[:\-]?\s*(FAC-?\d{4,8}|\d{5,8})", Opt);
    private static readonly Regex NumBl = new(@"(?:connaissement|b/?l|bill of lading)\s*(?:n[o°º]?|#|no\.?)?\s*[:\-]?\s*([A-Z]{2,4}-?\d{5,10})", Opt);
    private static readonly Regex NumBon = new(@"(?:bon de livraison|delivery note)\s*(?:n[o°º]?|#|no\.?)?\s*[:\-]?\s*(BL?V?-?\d{4,8})", Opt);
    private static readonly Regex Conteneur = new(@"\b([A-Z]{4}\s?\d{7})\b", Opt); // norme ISO 6346 : 4 lettres + 7 chiffres

    private static readonly Regex DateIso = new(@"\b(20\d{2})[-/.](0[1-9]|1[0-2])[-/.](0[1-9]|[12]\d|3[01])\b");
    private static readonly Regex DateFr = new(@"\b(0[1-9]|[12]\d|3[01])[-/.](0[1-9]|1[0-2])[-/.](20\d{2})\b");

    private static readonly Regex Client = new(@"(?:client|destinataire|consignee|livr[ée] [àa])\s*[:\-]\s*(.+)", Opt);
    // (?<![\w-]) : exclut "Sous-total" / "Subtotal" — on veut le total final, pas le sous-total.
    private static readonly Regex Total = new(@"(?<![\w-])(?:total(?: a payer| à payer)?|montant total|total due)\s*[:\-]?\s*([\d\s ]+[.,]\d{2})\s*\$?", Opt);

    public void Extract(CaptureDocument doc)
    {
        var text = doc.OcrText;
        var type = doc.Classification?.Type ?? DocumentType.Inconnu;

        var numero = type switch
        {
            DocumentType.Facture => Match(NumFacture, text),
            DocumentType.Connaissement => Match(NumBl, text),
            DocumentType.BonLivraison => Match(NumBon, text),
            _ => null
        };
        Add(doc, "numero", numero?.ToUpperInvariant().Replace(" ", ""));
        Add(doc, "date", ParseDate(text));
        Add(doc, "client", Match(Client, text)?.Trim().TrimEnd('.', ','));

        if (type == DocumentType.Facture)
            Add(doc, "montant", ParseAmount(Match(Total, text)));

        if (type == DocumentType.Connaissement)
            Add(doc, "conteneur", Match(Conteneur, text)?.Replace(" ", "").ToUpperInvariant());
    }

    private static void Add(CaptureDocument doc, string name, string? value)
        // Confiance simplifiée : 0.9 si trouvé, 0 sinon. Un vrai moteur combine confiance OCR + règle.
        => doc.Fields[name] = new ExtractedField(name, value, value is null ? 0 : 0.9);

    private static string? Match(Regex regex, string text)
    {
        var m = regex.Match(text);
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>Retourne la date au format ISO yyyy-MM-dd (format attendu par Alfresco d:date).</summary>
    public static string? ParseDate(string text)
    {
        var iso = DateIso.Match(text);
        if (iso.Success) return $"{iso.Groups[1].Value}-{iso.Groups[2].Value}-{iso.Groups[3].Value}";

        var fr = DateFr.Match(text);
        if (fr.Success) return $"{fr.Groups[3].Value}-{fr.Groups[2].Value}-{fr.Groups[1].Value}";

        return null;
    }

    /// <summary>"1 234,56" ou "1234.56" → "1234.56" (format invariant).</summary>
    public static string? ParseAmount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var cleaned = raw.Replace(" ", "").Replace(" ", "").Replace(" ", "").Replace(',', '.');
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
            ? d.ToString("0.00", CultureInfo.InvariantCulture)
            : null;
    }
}
