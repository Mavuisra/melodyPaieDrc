namespace MelodyPaieRDC.Services;

/// <summary>
/// Canal de mise à jour GitHub (dépôt Mavuisra/melodyPaieDrc).
/// Le dépôt doit être public pour que les clients accèdent au manifeste et aux Releases sans authentification.
/// </summary>
public static class ApplicationUpdateDefaults
{
    public const string GitHubRepo = "Mavuisra/melodyPaieDrc";

    public const string ManifestUrlParDefaut =
        "https://raw.githubusercontent.com/Mavuisra/melodyPaieDrc/main/installer/updates/version.json";

    public static string ReleasesLatestApiUrl =>
        $"https://api.github.com/repos/{GitHubRepo}/releases/latest";
}
