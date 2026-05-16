using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services.Export;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MelodyPaieRDC.Services;

public sealed class LivrePaieReglementaireExportService : PaieExportServiceBase
{
    public LivrePaieReglementaireExportService(PaieDbContext db) : base(db) { }

    public void ExporterExcel(int periodePaieId, string chemin)
    {
        var lot = Factory.ChargerLot(periodePaieId);
        var profil = ConfigurationExportsPaieService.Obtenir(Db).LivrePaieReglementaire;

        // Si le profil correspond au livre synthèse standard → même design Excel que le livre principal.
        if (EstProfilSyntheseStandard(profil))
        {
            var lignes = lot.Lignes.Select(ctx => new LivrePaieLigneExcel
            {
                Matricule = ctx.Employe?.Matricule ?? "",
                NomEmploye = $"{ctx.Employe?.Nom} {ctx.Employe?.Postnom} {ctx.Employe?.Prenom}".Trim(),
                Fonction = ExportDonneesPaieResolver.Resoudre(ctx, "Employe.Fonction"),
                SalaireBrut = ctx.Bulletin.TotalGainImposable + ctx.Bulletin.TotalGainNonImposable,
                Ipr = ctx.Bulletin.MontantIprNet,
                CnssEmploye = ctx.Bulletin.CotisationCnssOuvrier,
                CnssEmployeur = ctx.Cotisations?.CnssPatronal ?? 0,
                NetAPayer = ctx.Bulletin.NetAPayer
            }).ToList();
            LivrePaieExcelStylist.Generer(chemin, lot.Entreprise, lot.Periode, lignes);
            return;
        }

        var entete = new List<List<string>>
        {
            new() { "Livre de paie — modèle configurable" },
            new() { $"Période : {lot.Periode?.Mois:D2}/{lot.Periode?.Annee}" },
            new() { $"Entreprise : {lot.Entreprise?.RaisonSociale ?? ""}" }
        };
        ExportTabulaireWriter.GenererExcel(profil, chemin,
            $"Livre {lot.Periode?.Mois:D2}-{lot.Periode?.Annee}",
            ConstruireDonnees(lot, profil), entete);
    }

    private static bool EstProfilSyntheseStandard(ProfilExportConfig profil)
    {
        var actives = profil.Colonnes.Where(c => c.Actif).OrderBy(c => c.Ordre).Select(c => c.Code).ToList();
        var standard = new[] { "ORDRE", "MATRICULE", "NOM_COMPLET", "EMPLOI", "REMUNERATION", "IPR", "CNSS", "CNSS_PAT", "NET" };
        if (actives.Count != standard.Length) return false;
        for (int i = 0; i < standard.Length; i++)
        {
            if (!string.Equals(actives[i], standard[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    public void ExporterPdf(int periodePaieId, string chemin)
    {
        var lot = Factory.ChargerLot(periodePaieId);
        var profil = ConfigurationExportsPaieService.Obtenir(Db).LivrePaieReglementaire;
        var colonnes = profil.Colonnes.Where(c => c.Actif).OrderBy(c => c.Ordre).ToList();

        QuestPDF.Settings.License = LicenseType.Community;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Column(col =>
                {
                    col.Item().Text("Livre de paie").Bold().FontSize(14);
                    col.Item().Text($"{lot.Entreprise?.RaisonSociale} — {lot.Periode?.Mois:D2}/{lot.Periode?.Annee}").FontSize(10);
                    col.Item().Text("Colonnes selon configuration entreprise (réf. arrêté 08/08/2008)").Italic().FontSize(8);
                });

                page.Content().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        foreach (var _ in colonnes)
                            cols.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var col in colonnes)
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(2).Text(col.Libelle).Bold();
                    });

                    foreach (var ctx in lot.Lignes)
                    {
                        foreach (var col in colonnes)
                        {
                            var val = ExportDonneesPaieResolver.Resoudre(ctx, col.SourceDonnee, col);
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(val);
                        }
                    }
                });
            });
        }).GeneratePdf(chemin);
    }
}
