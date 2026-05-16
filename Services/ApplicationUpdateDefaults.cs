namespace MelodyPaieRDC.Services;

/// <summary>
/// Valeurs par défaut du canal de mise à jour (à aligner avec AppUpdatesURL dans installer/MelodyPaieRDC.iss).
/// </summary>
public static class ApplicationUpdateDefaults
{
    /// <summary>
    /// URL du manifeste JSON (version, downloadUrl, releaseNotes).
    /// Remplacez par votre hébergement réel avant distribution.
    /// </summary>
    public const string ManifestUrlParDefaut =
        "https://raw.githubusercontent.com/Mavuisra/melodyPaieDrc/main/installer/updates/version.json";
}
