using System.IO;
using System.Text.Json;
using MelodyPaieRDC.Data;

namespace MelodyPaieRDC.Helpers;

/// <summary>
/// Chargement / sauvegarde des paramètres SMTP (sans mot de passe) dans Data/smtp.json.
/// </summary>
public static class SmtpConfigHelper
{
    private static string ConfigPath => Path.Combine(PaieDbContext.GetDataDirectory(), "smtp.json");

    public static SmtpConfigDto? Charger()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return null;
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<SmtpConfigDto>(json);
        }
        catch
        {
            return null;
        }
    }

    public static void Sauvegarder(SmtpConfigDto config)
    {
        var dir = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}

public class SmtpConfigDto
{
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string SenderEmail { get; set; } = "";
}
