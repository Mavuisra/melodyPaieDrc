using System.Globalization;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>Situation mensuelle paie d'un agent (heures + rubriques bulletin).</summary>
public sealed class SituationPaieAgentLigne
{
    public int EmployeId { get; init; }
    public string Matricule { get; init; } = "";
    public string NomComplet { get; init; } = "";
    public string? Departement { get; init; }
    public decimal TotalHeures { get; init; }
    public decimal Salaire { get; init; }
    public decimal Quinzaine { get; init; }
    public decimal Retenue { get; init; }
    public decimal Impot { get; init; }
    public decimal Retards { get; init; }
    public decimal Prets { get; init; }
    public decimal SoldeAPayer { get; init; }
    public bool AvecBulletin { get; init; }

    private static string M(decimal v) => v.ToString("N2", CultureInfo.CurrentCulture);

    public string TotalHeuresLibelle => TotalHeures.ToString("N2", CultureInfo.CurrentCulture) + " h";
    public string SalaireLibelle => M(Salaire);
    public string QuinzaineLibelle => M(Quinzaine);
    public string RetenueLibelle => M(Retenue);
    public string ImpotLibelle => M(Impot);
    public string RetardsLibelle => M(Retards);
    public string PretsLibelle => M(Prets);
    public string SoldeAPayerLibelle => M(SoldeAPayer);
    public string StatutBulletinLibelle => AvecBulletin ? "Bulletin généré" : "Paie non calculée";
}

/// <summary>Construit les lignes de situation paie pour les rapports Heures.</summary>
public sealed class HeuresPaieRapportService
{
    public IReadOnlyList<SituationPaieAgentLigne> ConstruireSituationPeriode(PaieDbContext db, int periodePaieId)
    {
        var periode = db.PeriodesPaie.AsNoTracking().FirstOrDefault(p => p.Id == periodePaieId);
        if (periode is not { Mois: > 0, Annee: > 0 })
            return Array.Empty<SituationPaieAgentLigne>();

        var entrepriseId = ContexteEntrepriseService.ObtenirEntrepriseCouranteId(db);
        var politique = new PolitiquePaieService(db).Charger(entrepriseId);
        var libAcomptes = politique.LibelleRubrique("ACOMPTES_SALAIRE") ?? "Acomptes salaire";
        var libPrets = politique.LibelleRubrique("PRETS_AVANCES") ?? "Prêts / avances";
        var libSanctions = politique.LibelleRubrique("SANCTIONS_DISCIPLINAIRES") ?? "Sanctions / retards";

        var bulletins = db.BulletinsPaie
            .AsNoTracking()
            .AsSplitQuery()
            .Include(b => b.Details)
            .Include(b => b.Employe)
            .ThenInclude(e => e!.Departement)
            .Where(b => b.PeriodePaieId == periodePaieId)
            .ToDictionary(b => b.EmployeId);

        var saisies = db.SaisiesPaie
            .AsNoTracking()
            .Where(s => s.PeriodePaieId == periodePaieId)
            .ToDictionary(s => s.EmployeId);

        var employes = ContexteEntrepriseService.EmployesEntrepriseCourante(db)
            .AsNoTracking()
            .Include(e => e.Departement)
            .OrderBy(e => e.Matricule)
            .ToList();

        var debut = new DateTime(periode.Annee, periode.Mois, 1);
        var fin = debut.AddMonths(1).AddDays(-1);

        var lignes = new List<SituationPaieAgentLigne>(employes.Count);
        foreach (var e in employes)
        {
            bulletins.TryGetValue(e.Id, out var bulletin);
            saisies.TryGetValue(e.Id, out var saisie);
            var totaux = SuiviJournalierCalculPaieHelper.CalculerTotauxPresenceEmploye(db, e.Id, debut, fin);

            decimal salaire = 0, quinzaine = 0, retenue = 0, impot = 0, retards = 0, prets = 0, solde = 0;
            if (bulletin != null)
            {
                salaire = bulletin.SalaireBrut;
                impot = bulletin.MontantIprNet;
                retenue = bulletin.CotisationCnssOuvrier + bulletin.CotisationInpp;
                solde = bulletin.NetAPayer;
                quinzaine = RetenueDetail(bulletin, libAcomptes);
                prets = RetenueDetail(bulletin, libPrets);
                retards = RetenueDetail(bulletin, libSanctions);
            }
            else if (saisie != null)
            {
                quinzaine = saisie.AcomptesSalaire;
                retards = saisie.SanctionsDisciplinaires;
            }

            lignes.Add(new SituationPaieAgentLigne
            {
                EmployeId = e.Id,
                Matricule = e.Matricule,
                NomComplet = $"{e.Nom} {e.Postnom} {e.Prenom}".Trim(),
                Departement = e.Departement?.NomDepartement,
                TotalHeures = totaux.TotalHeures,
                Salaire = salaire,
                Quinzaine = quinzaine,
                Retenue = retenue,
                Impot = impot,
                Retards = retards,
                Prets = prets,
                SoldeAPayer = solde,
                AvecBulletin = bulletin != null
            });
        }

        return lignes;
    }

    private static decimal RetenueDetail(BulletinPaie bulletin, string libelle) =>
        bulletin.Details?
            .FirstOrDefault(d => string.Equals(d.Libelle, libelle, StringComparison.OrdinalIgnoreCase))
            ?.Retenue ?? 0m;
}
