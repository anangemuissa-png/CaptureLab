namespace CaptureLab.Core;

/// <summary>Choisit le moteur selon l'extension : .txt lu tel quel, images envoyées à l'OCR.</summary>
public sealed class CompositeOcrEngine(IOcrEngine imageEngine) : IOcrEngine
{
    private readonly TextFileOcrEngine _text = new();

    public static readonly string[] SupportedExtensions = [".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".txt"];

    public Task<string> ReadTextAsync(string path, CancellationToken ct = default) =>
        Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase)
            ? _text.ReadTextAsync(path, ct)
            : imageEngine.ReadTextAsync(path, ct);
}
