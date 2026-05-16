using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services.Export;

namespace MelodyPaieRDC.Services;

public sealed class DgiIprDeclarationExportService : PaieExportServiceBase
{
    public DgiIprDeclarationExportService(PaieDbContext db) : base(db) { }

    public string ExporterCsv(int periodePaieId)
    {
        var lot = Factory.ChargerLot(periodePaieId);
        VerifierExportAutorise(lot.Periode);
        var profil = ConfigurationExportsPaieService.Obtenir(Db).ExportIprDgi;
        var entete = ExportTabulaireWriter.ConstruireLignesEnteteEmployeur(lot.Entreprise, lot.Periode, profil);
        return ExportTabulaireWriter.GenererCsv(profil, ConstruireDonnees(lot, profil), entete);
    }

    public void ExporterExcel(int periodePaieId, string chemin)
    {
        var lot = Factory.ChargerLot(periodePaieId);
        VerifierExportAutorise(lot.Periode);
        var profil = ConfigurationExportsPaieService.Obtenir(Db).ExportIprDgi;
        var entete = ExportTabulaireWriter.ConstruireLignesEnteteEmployeur(lot.Entreprise, lot.Periode, profil);
        ExportTabulaireWriter.GenererExcel(profil, chemin,
            $"IPR DGI {lot.Periode?.Mois:D2}-{lot.Periode?.Annee}",
            ConstruireDonnees(lot, profil), entete);
    }

    public string ObtenirNomFichierSuggere(PeriodePaie? periode)
    {
        var ext = ConfigurationExportsPaieService.Obtenir(Db).ExportIprDgi.ExtensionFichier;
        return $"DGI_IPR_{periode?.Mois:D2}_{periode?.Annee}.{ext}";
    }
}
