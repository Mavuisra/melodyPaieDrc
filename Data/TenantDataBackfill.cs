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

        RenseignerEmployesEntrepriseIdDepuisRattachement(db);

        var ltId = db.Entreprises.IgnoreQueryFilters()
            .OrderBy(e => e.Id)
            .Select(e => e.Id)
            .FirstOrDefault();

        if (ltId > 0)
        {
            BackfillEtablissementsEntrepriseId(db);
            RattacherDonneesOrphelinesPremiereEntreprise(db, ltId);
        }

        BackfillPeriodesParEntreprise(db);
        BackfillReferentielsOrphelins(db);

        db.SaveChanges();
        HarmoniserEntrepriseActive(db);
    }

    /// <summary>
    /// Après restauration d'une sauvegarde : rattache établissements/employés orphelins et sélectionne
    /// l'entreprise qui contient réellement les données (évite un tableau de bord vide).
    /// </summary>
    public static int HarmoniserEntrepriseActive(PaieDbContext db)
    {
        if (!db.Entreprises.IgnoreQueryFilters().Any())
            return 0;

        BackfillEtablissementsEntrepriseId(db);
        BackfillEmployesEntrepriseId(db);
        db.SaveChanges();

        var entrepriseIds = db.Entreprises.IgnoreQueryFilters().OrderBy(e => e.Id).Select(e => e.Id).ToList();
        var candidat = ParametresApplicationHelper.GetDerniereEntrepriseActive(db)
                       ?? ContexteEntrepriseService.EntrepriseCouranteId
                       ?? 0;

        var effectifs = entrepriseIds
            .Select(id => new { Id = id, Nb = CompterEmployesPourEntreprise(db, id) })
            .OrderByDescending(x => x.Nb)
            .ThenBy(x => x.Id)
            .ToList();

        var totalEmployes = db.Employes.IgnoreQueryFilters().Count();
        int choix;
        if (totalEmployes == 0)
        {
            choix = candidat > 0 && entrepriseIds.Contains(candidat)
                ? candidat
                : entrepriseIds[0];
        }
        else if (candidat > 0 && effectifs.Any(e => e.Id == candidat && e.Nb > 0))
        {
            choix = candidat;
        }
        else
        {
            choix = effectifs.FirstOrDefault(e => e.Nb > 0)?.Id ?? entrepriseIds[0];
        }

        ContexteEntrepriseService.DefinirEntrepriseCourante(db, choix);
        return choix;
    }

    public static int CompterEmployesPourEntreprise(PaieDbContext db, int entrepriseId)
    {
        if (entrepriseId <= 0) return 0;

        return (
            from emp in db.Employes.IgnoreQueryFilters()
            join dep in db.Departements.IgnoreQueryFilters() on emp.DepartementId equals dep.Id into depJoin
            from dep in depJoin.DefaultIfEmpty()
            join et in db.Etablissements.IgnoreQueryFilters() on dep.EtablissementId equals et.Id into etJoin
            from et in etJoin.DefaultIfEmpty()
            where emp.EntrepriseId == entrepriseId
                  || (emp.EntrepriseId <= 0 && et != null && et.EntrepriseId == entrepriseId)
            select emp.Id
        ).Distinct().Count();
    }

    private static void BackfillEtablissementsEntrepriseId(PaieDbContext db)
    {
        var etabsSansEntreprise = db.Etablissements.IgnoreQueryFilters()
            .Where(e => e.EntrepriseId <= 0)
            .ToList();
        if (etabsSansEntreprise.Count == 0)
            return;

        var premiere = db.Entreprises.IgnoreQueryFilters().OrderBy(e => e.Id).Select(e => e.Id).FirstOrDefault();
        if (premiere <= 0)
            return;

        if (db.Entreprises.IgnoreQueryFilters().Count() == 1)
        {
            foreach (var et in etabsSansEntreprise)
                et.EntrepriseId = premiere;
            return;
        }

        foreach (var et in etabsSansEntreprise)
        {
            var depuisEmploye = (
                from d in db.Departements.IgnoreQueryFilters().Where(d => d.EtablissementId == et.Id)
                join emp in db.Employes.IgnoreQueryFilters() on d.Id equals emp.DepartementId
                where emp.EntrepriseId > 0
                select emp.EntrepriseId
            ).Distinct().FirstOrDefault();
            et.EntrepriseId = depuisEmploye > 0 ? depuisEmploye : premiere;
        }
    }

    /// <summary>Renseigne Employe.EntrepriseId depuis département → établissement.</summary>
    /// <summary>
    /// Renseigne EntrepriseId sans modifier les valeurs déjà présentes (données historiques).
    /// </summary>
    public static void BackfillEmployesEntrepriseId(PaieDbContext db)
    {
        var employes = db.Employes.IgnoreQueryFilters()
            .Include(e => e.Departement)
            .ThenInclude(d => d!.Etablissement)
            .Where(e => e.EntrepriseId <= 0)
            .ToList();

        foreach (var emp in employes)
        {
            var entrepriseId = emp.Departement?.Etablissement?.EntrepriseId ?? 0;
            if (entrepriseId > 0)
                emp.EntrepriseId = entrepriseId;
        }

        var orphelins = db.Employes.IgnoreQueryFilters().Where(e => e.EntrepriseId <= 0).ToList();
        if (orphelins.Count == 0)
            return;

        var idsEntreprises = db.Entreprises.IgnoreQueryFilters().OrderBy(e => e.Id).Select(e => e.Id).ToList();
        if (idsEntreprises.Count != 1)
            return;

        foreach (var emp in orphelins)
            emp.EntrepriseId = idsEntreprises[0];
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

    /// <summary>
    /// Après ajout de la colonne EntrepriseId, les lignes existantes restent NULL en SQLite :
    /// le filtre EF « &lt;= 0 » ne les voit pas. On renseigne depuis département → établissement.
    /// </summary>
    public static void RenseignerEmployesEntrepriseIdDepuisRattachement(PaieDbContext db)
    {
        if (!ColonneExiste(db, "Employes", "EntrepriseId"))
            return;

        db.Database.ExecuteSqlRaw("""
            UPDATE Employes
            SET EntrepriseId = COALESCE(
                (SELECT et.EntrepriseId
                 FROM Departements d
                 INNER JOIN Etablissements et ON et.Id = d.EtablissementId
                 WHERE d.Id = Employes.DepartementId),
                (SELECT Id FROM Entreprises ORDER BY Id LIMIT 1))
            WHERE EntrepriseId IS NULL OR EntrepriseId = 0
            """);
    }

    private static bool ColonneExiste(PaieDbContext db, string table, string colonne)
    {
        var conn = db.Database.GetDbConnection();
        var ouvertIci = conn.State != System.Data.ConnectionState.Open;
        if (ouvertIci)
            conn.Open();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (string.Equals(r.GetString(1), colonne, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        finally
        {
            if (ouvertIci)
                conn.Close();
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
