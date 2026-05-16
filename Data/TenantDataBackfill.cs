using System.Linq;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Data;

/// <summary>
/// Rattache les données historiques à la bonne entreprise pour l'isolation multi-tenant.
/// </summary>
public static class TenantDataBackfill
{
    public static void AppliquerSiNecessaire(PaieDbContext db)
    {
        if (!db.Entreprises.IgnoreQueryFilters().Any())
            return;

        var ltId = db.Entreprises.IgnoreQueryFilters()
            .OrderBy(e => e.Id)
            .Select(e => e.Id)
            .FirstOrDefault();

        if (ltId > 0)
            RattacherDonneesOrphelinesPremiereEntreprise(db, ltId);

        BackfillPeriodesParEntreprise(db);
        BackfillReferentielsOrphelins(db);

        db.SaveChanges();
    }

    /// <summary>Renseigne Employe.EntrepriseId depuis département → établissement.</summary>
    /// <summary>
    /// Renseigne EntrepriseId sans modifier les valeurs déjà présentes (données historiques).
    /// </summary>
    public static void BackfillEmployesEntrepriseId(PaieDbContext db)
    {
        var employes = db.Employes.IgnoreQueryFilters()
            .Include(e => e.Departement)!
            .ThenInclude(d => d.Etablissement)
            .Where(e => e.EntrepriseId <= 0)
            .ToList();

        foreach (var emp in employes)
        {
            var entrepriseId = emp.Departement?.Etablissement?.EntrepriseId ?? 0;
            if (entrepriseId > 0)
                emp.EntrepriseId = entrepriseId;
        }
    }

    /// <summary>
    /// Rattache les enregistrements orphelins (sans EntrepriseId) à la première entreprise de la base.
    /// Ne modifie pas l'entreprise active de la session (respecte DerniereEntrepriseActive).
    /// </summary>
    public static void RattacherDonneesOrphelinesPremiereEntreprise(PaieDbContext db, int premiereEntrepriseId)
    {
        if (premiereEntrepriseId <= 0) return;

        BackfillEmployesEntrepriseId(db);

        foreach (var p in db.PeriodesPaie.IgnoreQueryFilters()
                     .Where(x => x.EntrepriseId == null || x.EntrepriseId <= 0))
        {
            var depuisBulletins = db.BulletinsPaie.IgnoreQueryFilters()
                .Where(b => b.PeriodePaieId == p.Id && b.Employe != null && b.Employe.EntrepriseId == premiereEntrepriseId)
                .Any();
            if (depuisBulletins || !db.PeriodesPaie.IgnoreQueryFilters()
                    .Any(x => x.Id != p.Id && x.Mois == p.Mois && x.Annee == p.Annee && x.EntrepriseId == premiereEntrepriseId))
                p.EntrepriseId = premiereEntrepriseId;
        }

        AssignerEntrepriseIdAuxNull(
            db.GrillesIpr.IgnoreQueryFilters().Where(x => x.EntrepriseId == null), premiereEntrepriseId);
        AssignerEntrepriseIdAuxNull(
            db.TauxSociaux.IgnoreQueryFilters().Where(x => x.EntrepriseId == null), premiereEntrepriseId);
        AssignerEntrepriseIdAuxNull(
            db.ParametresIpr.IgnoreQueryFilters().Where(x => x.EntrepriseId == null), premiereEntrepriseId);
        AssignerEntrepriseIdAuxNull(
            db.CategoriesProfessionnelles.IgnoreQueryFilters().Where(x => x.EntrepriseId == null), premiereEntrepriseId);
        AssignerEntrepriseIdAuxNull(
            db.PrimesIndemnites.IgnoreQueryFilters().Where(x => x.EntrepriseId == null), premiereEntrepriseId);
        AssignerEntrepriseIdAuxNull(
            db.JoursTravailCalendrier.IgnoreQueryFilters().Where(x => x.EntrepriseId == null), premiereEntrepriseId);

        ParametresApplicationHelper.EnsureRowForEntreprise(db, premiereEntrepriseId);
    }

    private static void BackfillPeriodesParEntreprise(PaieDbContext db)
    {
        var periodes = db.PeriodesPaie.IgnoreQueryFilters()
            .Where(p => p.EntrepriseId == null || p.EntrepriseId <= 0)
            .ToList();

        foreach (var p in periodes)
        {
            var depuisBulletins = db.BulletinsPaie.IgnoreQueryFilters()
                .Where(b => b.PeriodePaieId == p.Id)
                .Select(b => b.Employe!.EntrepriseId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (depuisBulletins.Count == 1)
            {
                p.EntrepriseId = depuisBulletins[0];
                continue;
            }

            if (depuisBulletins.Count > 1)
            {
                // Période partagée par erreur : dupliquer une période par entreprise supplémentaire n'est pas fait ici ;
                // on rattache à la première entreprise trouvée (les bulletins restent filtrés par employé).
                p.EntrepriseId = depuisBulletins[0];
                continue;
            }

            var defautId = db.Entreprises.IgnoreQueryFilters().OrderBy(e => e.Id).Select(e => e.Id).FirstOrDefault();
            if (defautId > 0)
                p.EntrepriseId = defautId;
        }
    }

    private static void BackfillReferentielsOrphelins(PaieDbContext db)
    {
        var entreprises = db.Entreprises.IgnoreQueryFilters().OrderBy(e => e.Id).Select(e => e.Id).ToList();
        if (entreprises.Count == 0) return;

        var defautId = ContexteEntrepriseService.EntrepriseCouranteId ?? entreprises[0];

        AssignerEntrepriseIdAuxNull(db.GrillesIpr.IgnoreQueryFilters().Where(x => x.EntrepriseId == null), defautId);
        AssignerEntrepriseIdAuxNull(db.TauxSociaux.IgnoreQueryFilters().Where(x => x.EntrepriseId == null), defautId);
        AssignerEntrepriseIdAuxNull(db.ParametresIpr.IgnoreQueryFilters().Where(x => x.EntrepriseId == null), defautId);
        AssignerEntrepriseIdAuxNull(db.CategoriesProfessionnelles.IgnoreQueryFilters().Where(x => x.EntrepriseId == null), defautId);
        AssignerEntrepriseIdAuxNull(db.PrimesIndemnites.IgnoreQueryFilters().Where(x => x.EntrepriseId == null), defautId);
        AssignerEntrepriseIdAuxNull(db.JoursTravailCalendrier.IgnoreQueryFilters().Where(x => x.EntrepriseId == null), defautId);

        foreach (var entrepriseId in entreprises)
        {
            DonneesPaieReferenceSeed.SeedCategoriesSiVide(db, entrepriseId);
            DonneesPaieReferenceSeed.SeedReferentielLegalSiVide(db, entrepriseId);
            DonneesPaieReferenceSeed.SeedPrimesCourantesSiVide(db, entrepriseId);
            ParametresApplicationHelper.EnsureRowForEntreprise(db, entrepriseId);
        }
    }

    private static void AssignerEntrepriseIdAuxNull<T>(IEnumerable<T> rows, int entrepriseId) where T : class
    {
        foreach (var row in rows)
        {
            switch (row)
            {
                case GrilleIPR g: g.EntrepriseId = entrepriseId; break;
                case TauxSociaux t: t.EntrepriseId = entrepriseId; break;
                case ParametreIPR p: p.EntrepriseId = entrepriseId; break;
                case CategorieProfessionnelle c: c.EntrepriseId = entrepriseId; break;
                case PrimeIndemnite pr: pr.EntrepriseId = entrepriseId; break;
                case JourTravailCalendrier j: j.EntrepriseId = entrepriseId; break;
            }
        }
    }

    public static int ResoudreEntrepriseIdDepuisDepartement(PaieDbContext db, int departementId)
    {
        if (departementId <= 0) return 0;
        return db.Departements.IgnoreQueryFilters()
            .Where(d => d.Id == departementId)
            .Select(d => d.Etablissement!.EntrepriseId)
            .FirstOrDefault();
    }
}
