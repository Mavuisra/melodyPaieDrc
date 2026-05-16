using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services.Export;

namespace MelodyPaieRDC.Services;

public sealed class VirementBancaireExportService : PaieExportServiceBase
{
    public VirementBancaireExportService(PaieDbContext db) : base(db) { }

    public string ExporterCsv(int periodePaieId, string? codeProfilBanque = null)
    {
        var lot = Factory.ChargerLot(periodePaieId);
        VerifierExportAutorise(lot.Periode);
        var profilBanque = ConfigurationExportsPaieService.ObtenirProfilVirement(Db, codeProfilBanque)
            ?? throw new InvalidOperationException("Aucun profil de virement bancaire configuré.");

        var profil = new ProfilExportConfig
        {
            Code = profilBanque.Code,
            Libelle = profilBanque.Libelle,
            TypeFormat = profilBanque.TypeFormat,
            Separateur = profilBanque.Separateur,
            ExtensionFichier = profilBanque.ExtensionFichier,
            Colonnes = profilBanque.Colonnes
        };

        var lignes = lot.Lignes
            .Where(ctx => ctx.Bulletin.NetAPayer > 0)
            .Select(ctx => ExportTabulaireWriter.ConstruireLigne(ctx, profil));

        return ExportTabulaireWriter.GenererCsv(profil, lignes);
    }

    public void ExporterExcel(int periodePaieId, string chemin, string? codeProfilBanque = null)
    {
        var csvProfil = ConfigurationExportsPaieService.ObtenirProfilVirement(Db, codeProfilBanque)!;
        var profil = new ProfilExportConfig
        {
            Code = csvProfil.Code,
            Libelle = csvProfil.Libelle,
            Colonnes = csvProfil.Colonnes,
            ExtensionFichier = "xlsx"
        };
        var lot = Factory.ChargerLot(periodePaieId);
        var donnees = lot.Lignes
            .Where(ctx => ctx.Bulletin.NetAPayer > 0)
            .Select(ctx => ExportTabulaireWriter.ConstruireLigne(ctx, profil));
        ExportTabulaireWriter.GenererExcel(profil, chemin,
            $"Virements {lot.Periode?.Mois:D2}-{lot.Periode?.Annee}", donnees);
    }

    public string ObtenirNomFichierSuggere(PeriodePaie? periode, string? codeProfil)
    {
        var p = ConfigurationExportsPaieService.ObtenirProfilVirement(Db, codeProfil);
        return $"Virements_{p?.Code ?? "banque"}_{periode?.Mois:D2}_{periode?.Annee}.{p?.ExtensionFichier ?? "csv"}";
    }
}
