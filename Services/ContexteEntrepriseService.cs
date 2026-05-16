using System.Linq;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Contexte multi-entreprise de la session (entreprise courante).
/// </summary>
public static class ContexteEntrepriseService
{
    public static int? EntrepriseCouranteId { get; set; }

    public static int ObtenirEntrepriseCouranteId(PaieDbContext db)
    {
        if (EntrepriseCouranteId is > 0)
            return EntrepriseCouranteId.Value;

        var id = db.Entreprises.OrderBy(e => e.Id).Select(e => e.Id).FirstOrDefault();
        if (id > 0)
            EntrepriseCouranteId = id;
        return id;
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
}
