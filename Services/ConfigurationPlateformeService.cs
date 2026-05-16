using System.Text.Json;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Charge et enregistre la configuration plateforme (modules, colonnes, etc.) par entreprise.
/// </summary>
public static class ConfigurationPlateformeService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static ConfigurationPlateforme Charger(PaieDbContext db)
    {
        var p = ParametresApplicationHelper.GetParametresEntrepriseCourante(db);
        if (string.IsNullOrWhiteSpace(p.ConfigurationUiJson))
            return new ConfigurationPlateforme();

        try
        {
            return JsonSerializer.Deserialize<ConfigurationPlateforme>(p.ConfigurationUiJson, JsonOptions)
                   ?? new ConfigurationPlateforme();
        }
        catch
        {
            return new ConfigurationPlateforme();
        }
    }

    public static void Enregistrer(PaieDbContext db, ConfigurationPlateforme config)
    {
        var p = ParametresApplicationHelper.GetParametresEntrepriseCourante(db);
        p.ConfigurationUiJson = JsonSerializer.Serialize(config, JsonOptions);
        p.DateDerniereModification = DateTime.UtcNow;
        db.SaveChanges();
    }

    public static bool ModuleActif(PaieDbContext db, string codeModule)
    {
        var cfg = Charger(db);
        return !cfg.ModulesActifs.TryGetValue(codeModule, out var actif) || actif;
    }

    public static bool ColonneEmployeVisible(PaieDbContext db, string codeColonne)
    {
        var cfg = Charger(db);
        return !cfg.ColonnesListeEmployes.TryGetValue(codeColonne, out var visible) || visible;
    }
}
