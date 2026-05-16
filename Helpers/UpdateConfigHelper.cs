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
            UpdateConfigDto dto;
            if (!File.Exists(ConfigPath))
                dto = CreerDefaut();
            else
            {
                var json = File.ReadAllText(ConfigPath);
                dto = JsonSerializer.Deserialize<UpdateConfigDto>(json) ?? new UpdateConfigDto();
                if (string.IsNullOrWhiteSpace(dto.ManifestUrl))
                    dto.ManifestUrl = ApplicationUpdateDefaults.ManifestUrlParDefaut;
                else
                    dto.ManifestUrl = dto.ManifestUrl.Trim();
            }

            if (CorrigerUrlObsolete(dto))
            {
                try { Sauvegarder(dto); } catch { /* ignore */ }
            }

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

    /// <summary>Remplace example.com ou URL vide par le manifeste GitHub officiel.</summary>
    private static bool CorrigerUrlObsolete(UpdateConfigDto dto)
    {
        var url = dto.ManifestUrl ?? "";
        if (string.IsNullOrWhiteSpace(url) ||
            url.Contains("example.com", StringComparison.OrdinalIgnoreCase) ||
            !url.Contains("github", StringComparison.OrdinalIgnoreCase))
        {
            dto.ManifestUrl = ApplicationUpdateDefaults.ManifestUrlParDefaut;
            return true;
        }
        return false;
    }

    private static UpdateConfigDto CreerDefaut()
    {
        var dto = new UpdateConfigDto
        {
            ManifestUrl = ApplicationUpdateDefaults.ManifestUrlParDefaut,
            VerifierAuDemarrage = true
        };
        try { Sauvegarder(dto); } catch { /* ignore */ }
        return dto;
    }
}

public class UpdateConfigDto
{
    public string ManifestUrl { get; set; } = ApplicationUpdateDefaults.ManifestUrlParDefaut;

    public bool VerifierAuDemarrage { get; set; } = true;
}
