using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services.Export;

namespace MelodyPaieRDC.Services;

public sealed class CnssEDeclarationExportService : PaieExportServiceBase
{
    public CnssEDeclarationExportService(PaieDbContext db) : base(db) { }

    public string ExporterCsv(int periodePaieId)
    {
        var lot = Factory.ChargerLot(periodePaieId);
        VerifierExportAutorise(lot.Periode);
        var profil = ConfigurationExportsPaieService.Obtenir(Db).ExportCnssEdeclaration;
        var entete = ExportTabulaireWriter.ConstruireLignesEnteteEmployeur(lot.Entreprise, lot.Periode, profil);
        return ExportTabulaireWriter.GenererCsv(profil, ConstruireDonnees(lot, profil), entete);
    }

    public void ExporterExcel(int periodePaieId, string chemin)
    {
        var lot = Factory.ChargerLot(periodePaieId);
        VerifierExportAutorise(lot.Periode);
        var profil = ConfigurationExportsPaieService.Obtenir(Db).ExportCnssEdeclaration;
        profil.TypeFormat = "Excel";
        var entete = ExportTabulaireWriter.ConstruireLignesEnteteEmployeur(lot.Entreprise, lot.Periode, profil);
        var titre = $"CNSS {lot.Periode?.Mois:D2}-{lot.Periode?.Annee}";
        ExportTabulaireWriter.GenererExcel(profil, chemin, titre, ConstruireDonnees(lot, profil), entete);
    }

    public string ObtenirNomFichierSuggere(PeriodePaie? periode)
    {
        var ext = ConfigurationExportsPaieService.Obtenir(Db).ExportCnssEdeclaration.ExtensionFichier;
        return $"CNSS_edeclaration_{periode?.Mois:D2}_{periode?.Annee}.{ext}";
    }
}
