using System.Linq;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Contexte multi-entreprise de la session (entreprise courante).
/// Toutes les listes métier doivent passer par <see cref="EmployesEntrepriseCourante"/>.
/// </summary>
public static class ContexteEntrepriseService
{
    public static int? EntrepriseCouranteId { get; set; }

    public static void InitialiserDepuisBase(PaieDbContext db)
    {
        ParametresApplicationHelper.EnsureRow(db);
        var derniere = ParametresApplicationHelper.GetDerniereEntrepriseActive(db);
        if (derniere is > 0
            && db.Entreprises.IgnoreQueryFilters().Any(e => e.Id == derniere.Value))
        {
            EntrepriseCouranteId = derniere.Value;
            return;
        }

        _ = ObtenirEntrepriseCouranteId(db);
    }

    public static int ObtenirEntrepriseCouranteId(PaieDbContext db)
    {
        if (EntrepriseCouranteId is > 0
            && db.Entreprises.IgnoreQueryFilters().Any(e => e.Id == EntrepriseCouranteId.Value))
            return EntrepriseCouranteId.Value;

        var id = db.Entreprises.IgnoreQueryFilters().OrderBy(e => e.Id).Select(e => e.Id).FirstOrDefault();
        if (id > 0)
            DefinirEntrepriseCourante(db, id);
        return id;
    }

    public static void DefinirEntrepriseCourante(int entrepriseId)
    {
        if (entrepriseId <= 0) return;
        EntrepriseCouranteId = entrepriseId;
    }

    public static void DefinirEntrepriseCourante(PaieDbContext db, int entrepriseId)
    {
        if (entrepriseId <= 0) return;
        EntrepriseCouranteId = entrepriseId;
        ParametresApplicationHelper.SetDerniereEntrepriseActive(db, entrepriseId);
    }

    public static string? ObtenirRaisonSocialeCourante(PaieDbContext db)
    {
        var id = ObtenirEntrepriseCouranteId(db);
        if (id <= 0) return null;
        return db.Entreprises.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => e.RaisonSociale)
            .FirstOrDefault();
    }

    /// <summary>Employés rattachés à l'entreprise courante (via département → établissement).</summary>
    public static IQueryable<Employe> EmployesEntrepriseCourante(PaieDbContext db)
    {
        var entrepriseId = ObtenirEntrepriseCouranteId(db);
        if (entrepriseId <= 0)
            return db.Employes.Where(_ => false);

        return
            from emp in db.Employes
            join dep in db.Departements on emp.DepartementId equals dep.Id
            join et in db.Etablissements on dep.EtablissementId equals et.Id
            where et.EntrepriseId == entrepriseId
            select emp;
    }

    public static IQueryable<Departement> DepartementsEntrepriseCourante(PaieDbContext db)
    {
        var entrepriseId = ObtenirEntrepriseCouranteId(db);
        if (entrepriseId <= 0)
            return db.Departements.Where(_ => false);

        return
            from dep in db.Departements
            join et in db.Etablissements on dep.EtablissementId equals et.Id
            where et.EntrepriseId == entrepriseId
            select dep;
    }

    public static IQueryable<Etablissement> EtablissementsEntrepriseCourante(PaieDbContext db)
    {
        var entrepriseId = ObtenirEntrepriseCouranteId(db);
        if (entrepriseId <= 0)
            return db.Etablissements.Where(_ => false);

        return db.Etablissements.Where(e => e.EntrepriseId == entrepriseId);
    }

    public static int ObtenirEntrepriseIdEmploye(PaieDbContext db, int employeId)
    {
        var query =
            from e in db.Employes
            where e.Id == employeId
            join d in db.Departements on e.DepartementId equals d.Id
            join et in db.Etablissements on d.EtablissementId equals et.Id
            select et.EntrepriseId;

        var entrepriseId = query.FirstOrDefault();
        return entrepriseId > 0 ? entrepriseId : ObtenirEntrepriseCouranteId(db);
    }

    public static bool EmployeAppartientEntrepriseCourante(PaieDbContext db, int employeId)
    {
        var courante = ObtenirEntrepriseCouranteId(db);
        if (courante <= 0) return false;
        return ObtenirEntrepriseIdEmploye(db, employeId) == courante;
    }
}
