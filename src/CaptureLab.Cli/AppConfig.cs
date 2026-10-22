using System.Text.Json;
using CaptureLab.Alfresco;

namespace CaptureLab.Cli;

public sealed class OcrConfig
{
    public string TesseractPath { get; set; } = "tesseract";
    public string Languages { get; set; } = "fra+eng";
}

public sealed class CaptureConfig
{
    public double ClassificationThreshold { get; set; } = 0.6;
    public bool MoveFiles { get; set; } = true;
}

public sealed class AppConfig
{
    public string Environment { get; set; } = "Development";
    public AlfrescoOptions Alfresco { get; set; } = new();
    public OcrConfig Ocr { get; set; } = new();
    public CaptureConfig Capture { get; set; } = new();

    /// <summary>
    /// Ordre de chargement (le dernier gagne) :
    ///   1. appsettings.json
    ///   2. appsettings.{Environnement}.json  (Development = serveur de TEST, Production = serveur de PROD)
    ///   3. variables d'environnement ALFRESCO_BASEURL / ALFRESCO_USERNAME / ALFRESCO_PASSWORD
    /// Même principe que la séparation test/production du projet de 2023.
    /// </summary>
    public static AppConfig Load()
    {
        var env = System.Environment.GetEnvironmentVariable("CAPTURELAB_ENVIRONMENT") ?? "Development";
        var baseDir = AppContext.BaseDirectory;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip };

        var config = new AppConfig();
        foreach (var file in new[] { "appsettings.json", $"appsettings.{env}.json" })
        {
            var path = Path.Combine(baseDir, file);
            if (!File.Exists(path)) continue;
            var layer = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), options);
            if (layer is not null) config = Merge(config, layer, File.ReadAllText(path));
        }

        config.Environment = env;
        config.Alfresco.BaseUrl = Env("ALFRESCO_BASEURL") ?? config.Alfresco.BaseUrl;
        config.Alfresco.Username = Env("ALFRESCO_USERNAME") ?? config.Alfresco.Username;
        config.Alfresco.Password = Env("ALFRESCO_PASSWORD") ?? config.Alfresco.Password;
        return config;
    }

    private static string? Env(string name) =>
        System.Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;

    // Fusion simple : une section présente dans le fichier remplace la section précédente propriété par propriété.
    private static AppConfig Merge(AppConfig baseCfg, AppConfig layer, string rawJson)
    {
        using var doc = JsonDocument.Parse(rawJson, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
        foreach (var section in doc.RootElement.EnumerateObject())
        {
            object? target = section.Name.ToLowerInvariant() switch
            {
                "alfresco" => baseCfg.Alfresco,
                "ocr" => baseCfg.Ocr,
                "capture" => baseCfg.Capture,
                _ => null
            };
            object? source = section.Name.ToLowerInvariant() switch
            {
                "alfresco" => layer.Alfresco,
                "ocr" => layer.Ocr,
                "capture" => layer.Capture,
                _ => null
            };
            if (target is null || source is null || section.Value.ValueKind != JsonValueKind.Object) continue;

            foreach (var prop in section.Value.EnumerateObject())
            {
                var pi = target.GetType().GetProperty(prop.Name,
                    System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (pi is { CanWrite: true }) pi.SetValue(target, pi.GetValue(source));
            }
        }
        return baseCfg;
    }
}
