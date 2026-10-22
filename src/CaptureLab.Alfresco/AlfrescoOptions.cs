namespace CaptureLab.Alfresco;

/// <summary>
/// Paramètres de connexion. Aucun secret dans le code ni dans appsettings.json :
/// le nom d'utilisateur et le mot de passe viennent des variables d'environnement
/// ALFRESCO_USERNAME et ALFRESCO_PASSWORD (voir README).
/// </summary>
public sealed class AlfrescoOptions
{
    /// <summary>Racine du serveur, sans "/alfresco" (ex. le poste local en test).</summary>
    public string BaseUrl { get; set; } = "http://localhost:8080";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    /// <summary>Nœud parent : alias "-shared-" (Dossiers partagés), "-my-" (Mes fichiers), "-root-" ou un nodeId.</summary>
    public string ParentNode { get; set; } = "-shared-";

    /// <summary>Dossier racine de la capture, sous le nœud parent.</summary>
    public string RootFolder { get; set; } = "Capture";

    /// <summary>
    /// Aspect personnalisé créé dans le Model Manager (ex. "cap:documentCapture").
    /// Vide = on n'utilise que les propriétés standard cm:title / cm:description + étiquettes.
    /// </summary>
    public string CustomAspect { get; set; } = "";

    /// <summary>Préfixe des propriétés du modèle personnalisé (ex. "cap").</summary>
    public string CustomPrefix { get; set; } = "cap";

    public int TimeoutSeconds { get; set; } = 100;
    public int MaxRetries { get; set; } = 3;

    public string ApiRoot => BaseUrl.TrimEnd('/') + "/alfresco/api/-default-/public";
}
