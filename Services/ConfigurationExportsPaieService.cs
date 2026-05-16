using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

public static class ConfigurationExportsPaieService
{
    public static ConfigurationExportsPaie Obtenir(PaieDbContext db)
    {
        var plateforme = ConfigurationPlateformeService.Charger(db);
        plateforme.ExportsPaie = ConfigurationExportsPaieDefaults.Fusionner(plateforme.ExportsPaie);
        return plateforme.ExportsPaie;
    }

    public static void Enregistrer(PaieDbContext db, ConfigurationExportsPaie exports)
    {
        var plateforme = ConfigurationPlateformeService.Charger(db);
        plateforme.ExportsPaie = exports;
        ConfigurationPlateformeService.Enregistrer(db, plateforme);
    }

    public static ProfilBanqueVirement? ObtenirProfilVirement(PaieDbContext db, string? code = null)
    {
        var cfg = Obtenir(db);
        var c = code ?? cfg.CodeProfilVirementParDefaut;
        return cfg.ProfilsVirement.FirstOrDefault(p =>
                   string.Equals(p.Code, c, StringComparison.OrdinalIgnoreCase))
               ?? cfg.ProfilsVirement.FirstOrDefault();
    }
}
