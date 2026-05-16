using System.Text.Json.Serialization;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Manifeste de version hébergé sur le serveur de mises à jour (fichier version.json).
/// </summary>
public class UpdateManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = "";

    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }

    [JsonPropertyName("publishedAt")]
    public string? PublishedAt { get; set; }

    /// <summary>Empreinte SHA-256 hexadécimale (optionnelle) du fichier d'installation.</summary>
    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }
}
