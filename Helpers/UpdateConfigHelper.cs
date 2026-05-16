using System.IO;
using System.Text.Json;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.Helpers;

/// <summary>
/// Configuration locale des mises à jour (URL du manifeste version.json).
/// Fichier : %LocalAppData%\MelodyPaieRDC\Data\updates-config.json
/// </summary>
public static class UpdateConfigHelper
{
    private static string ConfigPath => Path.Combine(PaieDbContext.GetDataDirectory(), "updates-config.json");

    public static UpdateConfigDto Charger()
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return CreerDefaut();
            var json = File.ReadAllText(ConfigPath);
            var dto = JsonSerializer.Deserialize<UpdateConfigDto>(json);
            if (dto == null || string.IsNullOrWhiteSpace(dto.ManifestUrl))
                return CreerDefaut();
            dto.ManifestUrl = dto.ManifestUrl.Trim();
            return dto;
        }
        catch
        {
            return CreerDefaut();
        }
    }

    public static void Sauvegarder(UpdateConfigDto config)
    {
        var dir = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    private static UpdateConfigDto CreerDefaut()
    {
        var dto = new UpdateConfigDto
        {
            ManifestUrl = ApplicationUpdateDefaults.ManifestUrlParDefaut,
            VerifierAuDemarrage = true
        };
        try
        {
            Sauvegarder(dto);
        }
        catch
        {
            // Lecture seule ou droits : on retourne quand même les valeurs par défaut en mémoire.
        }
        return dto;
    }
}

public class UpdateConfigDto
{
    public string ManifestUrl { get; set; } = ApplicationUpdateDefaults.ManifestUrlParDefaut;

    /// <summary>Si true, propose une mise à jour au démarrage lorsqu'une version plus récente est disponible.</summary>
    public bool VerifierAuDemarrage { get; set; } = true;
}
