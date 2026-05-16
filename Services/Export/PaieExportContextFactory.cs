using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services.Export;

public sealed class PaieExportContextFactory
{
    private readonly PaieDbContext _db;
    private readonly CotisationsSocialesService _cotisations;

    public PaieExportContextFactory(PaieDbContext db)
    {
        _db = db;
        _cotisations = new CotisationsSocialesService(db);
    }

    public PaieExportLot ChargerLot(int periodePaieId)
    {
        var bulletins = _db.BulletinsPaie
            .AsNoTracking()
            .AsSplitQuery()
            .Include(b => b.Employe)
            .ThenInclude(e => e!.Departement)
            .ThenInclude(d => d!.Etablissement)
            .Include(b => b.Employe)
            .ThenInclude(e => e!.AyantsDroit)
            .Include(b => b.PeriodePaie)
            .Include(b => b.Details)
            .Where(b => b.PeriodePaieId == periodePaieId)
            .OrderBy(b => b.Employe!.Matricule)
            .ToList();

        var periode = bulletins.FirstOrDefault()?.PeriodePaie
                      ?? _db.PeriodesPaie.AsNoTracking().FirstOrDefault(p => p.Id == periodePaieId);

        var entrepriseId = ContexteEntrepriseService.ObtenirEntrepriseCouranteId(_db);
        var entreprise = entrepriseId > 0
            ? _db.Entreprises.AsNoTracking().FirstOrDefault(e => e.Id == entrepriseId)
            : null;

        var employeIds = bulletins.Select(b => b.EmployeId).Distinct().ToList();
        var contrats = _db.Contrats
            .AsNoTracking()
            .Include(c => c.CategorieProfessionnelle)
            .Where(c => employeIds.Contains(c.EmployeId))
            .OrderByDescending(c => c.DateDebut)
            .ToList()
            .GroupBy(c => c.EmployeId)
            .ToDictionary(g => g.Key, g => g.First());

        var saisies = _db.SaisiesPaie
            .AsNoTracking()
            .Where(s => s.PeriodePaieId == periodePaieId)
            .ToDictionary(s => s.EmployeId);

        var heuresParEmploye = new Dictionary<int, decimal>();
        if (periode is { Mois: > 0, Annee: > 0 })
        {
            foreach (var employeId in employeIds)
            {
                heuresParEmploye[employeId] = SuiviJournalierPdfDataService.CalculerTotalHeuresPourEmploye(
                    _db, employeId, periode.Mois, periode.Annee);
            }
        }

        var contexts = new List<ExportDonneesPaieContext>();
        int ordre = 1;
        foreach (var b in bulletins)
        {
            contrats.TryGetValue(b.EmployeId, out var contrat);
            saisies.TryGetValue(b.EmployeId, out var saisie);
            var baseCnss = ObtenirBaseCnss(b);
            var cot = _cotisations.Calculer(baseCnss);
            var nbEnfants = b.Employe?.AyantsDroit?.Count(a =>
                a.LienParente.Contains("enfant", StringComparison.OrdinalIgnoreCase)) ?? 0;
            heuresParEmploye.TryGetValue(b.EmployeId, out var heuresPeriode);

            contexts.Add(new ExportDonneesPaieContext
            {
                Bulletin = b,
                Contrat = contrat,
                Saisie = saisie,
                Entreprise = entreprise,
                NumeroOrdre = ordre++,
                NbEnfants = nbEnfants,
                HeuresTravailPeriode = heuresPeriode,
                CommuneAffectation = b.Employe?.CommuneAffectation,
                Cotisations = new CotisationsCalculees
                {
                    BaseCnss = baseCnss,
                    CnssOuvrier = b.CotisationCnssOuvrier,
                    CnssPatronal = cot.CnssPatronal,
                    Inpp = b.CotisationInpp,
                    Onem = cot.Onem
                },
                ReferenceVirement = $"{ConfigurationExportsPaieService.Obtenir(_db).ProfilsVirement.FirstOrDefault()?.PrefixeReference ?? "PAIE"}-{periode?.Annee}{periode?.Mois:D2}-{b.Employe?.Matricule}"
            });
        }

        return new PaieExportLot(periode, entreprise, contexts);
    }

    private static decimal ObtenirBaseCnss(BulletinPaie bulletin)
    {
        var ligne = bulletin.Details?.FirstOrDefault(d =>
            d.Libelle.Contains("CNSS", StringComparison.OrdinalIgnoreCase) &&
            d.Libelle.Contains("ouvr", StringComparison.OrdinalIgnoreCase));
        if (ligne?.BaseCalcul > 0) return ligne.BaseCalcul;
        return bulletin.TotalGainImposable + bulletin.TotalGainNonImposable;
    }
}

public sealed class PaieExportLot
{
    public PeriodePaie? Periode { get; }
    public Entreprise? Entreprise { get; }
    public IReadOnlyList<ExportDonneesPaieContext> Lignes { get; }

    public PaieExportLot(PeriodePaie? periode, Entreprise? entreprise, List<ExportDonneesPaieContext> lignes)
    {
        Periode = periode;
        Entreprise = entreprise;
        Lignes = lignes;
    }
}
