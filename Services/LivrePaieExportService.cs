using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services.Export;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Export du livre de paie synthèse (Excel designé, PDF via ExportPdfService).
/// </summary>
public class LivrePaieExportService
{
    private readonly PaieDbContext _db;

    public LivrePaieExportService(PaieDbContext db) => _db = db;

    /// <summary>
    /// Exporte le livre de paie pour la période (colonnes : N°, Matricule, Nom, Fonction, Brut, IPR, CNSS, Net).
    /// </summary>
    public void ExporterExcel(int periodePaieId, string cheminFichier)
    {
        var lot = new PaieExportContextFactory(_db).ChargerLot(periodePaieId);
        var lignes = lot.Lignes.Select(ctx => new LivrePaieLigneExcel
        {
            Matricule = ctx.Employe?.Matricule ?? "",
            NomEmploye = NomComplet(ctx.Employe),
            Fonction = ResoudreFonction(ctx),
            SalaireBrut = ctx.Bulletin.TotalGainImposable + ctx.Bulletin.TotalGainNonImposable,
            Ipr = ctx.Bulletin.MontantIprNet,
            CnssEmploye = ctx.Bulletin.CotisationCnssOuvrier,
            CnssEmployeur = ctx.Cotisations?.CnssPatronal ?? 0,
            NetAPayer = ctx.Bulletin.NetAPayer
        }).ToList();

        LivrePaieExcelStylist.Generer(cheminFichier, lot.Entreprise, lot.Periode, lignes);
    }

    private static string NomComplet(Models.Employe? e) =>
        e is null ? "" : $"{e.Nom} {e.Postnom} {e.Prenom}".Trim();

    private static string ResoudreFonction(ExportDonneesPaieContext ctx)
    {
        var cat = ctx.Contrat?.CategorieProfessionnelle?.Libelle;
        if (!string.IsNullOrWhiteSpace(cat)) return cat;
        return ctx.Employe?.Departement?.NomDepartement ?? "";
    }
}
