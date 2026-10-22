using System.Diagnostics;
using System.Text;

namespace CaptureLab.Core;

/// <summary>
/// Abstraction du moteur OCR. En 2023 l'OCR était fait en WinDev ; ici on l'isole derrière
/// une interface pour pouvoir changer de moteur (Tesseract, Azure Document Intelligence, Tungsten…)
/// sans toucher au reste de la chaîne.
/// </summary>
public interface IOcrEngine
{
    Task<string> ReadTextAsync(string imagePath, CancellationToken ct = default);
}

/// <summary>
/// OCR via l'exécutable Tesseract (gratuit, multiplateforme).
/// Windows : installer Tesseract (build UB Mannheim) avec le pack de langue français, puis
/// indiquer le chemin complet de tesseract.exe dans appsettings.json si ce n'est pas dans le PATH.
/// </summary>
public sealed class TesseractCliOcrEngine : IOcrEngine
{
    private readonly string _exePath;
    private readonly string _languages;

    public TesseractCliOcrEngine(string exePath = "tesseract", string languages = "fra+eng")
    {
        _exePath = exePath;
        _languages = languages;
    }

    public async Task<string> ReadTextAsync(string imagePath, CancellationToken ct = default)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Image introuvable pour l'OCR", imagePath);

        var psi = new ProcessStartInfo
        {
            FileName = _exePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,   // Tesseract écrit en UTF-8
            StandardErrorEncoding = Encoding.UTF8
        };
        // "stdout" = Tesseract écrit le texte sur la sortie standard au lieu d'un fichier
        psi.ArgumentList.Add(imagePath);
        psi.ArgumentList.Add("stdout");
        psi.ArgumentList.Add("-l");
        psi.ArgumentList.Add(_languages);
        psi.ArgumentList.Add("--psm");
        psi.ArgumentList.Add("4"); // page en une seule colonne de taille variable : bon pour factures / BL

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Impossible de lancer '{_exePath}'. Tesseract est-il installé ?");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Tesseract a échoué (code {process.ExitCode}) : {await stderrTask}");

        return await stdoutTask;
    }
}

/// <summary>
/// Moteur "texte" : lit un .txt déjà OCRisé. Pratique pour les tests et pour simuler
/// un OCR externe (ex. texte reçu d'un autre système).
/// </summary>
public sealed class TextFileOcrEngine : IOcrEngine
{
    public Task<string> ReadTextAsync(string path, CancellationToken ct = default) => File.ReadAllTextAsync(path, ct);
}
