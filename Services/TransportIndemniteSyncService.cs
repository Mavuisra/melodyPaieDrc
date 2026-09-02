using MelodyPaieRDC.Data;
using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Aligne l'indemnité de transport sur 62,40 $ / mois pour les employés non stagiaires.
/// Les stagiaires (contrat Stage / Stagiaire) n'ont pas d'indemnité de transport.
/// </summary>
public static class TransportIndemniteSyncService
{
    public static int SynchroniserPourTous(PaieDbContext db, decimal? montant = null)
    {
        var cible = montant ?? TransportIndemniteDefaults.MontantMensuelUsd;
        var primesTransport = db.PrimesIndemnites
            .AsNoTracking()
            .ToList()
            .Where(p => TransportAbsencePaieHelper.EstIndemniteTransport(p.Libelle))
            .Select(p => p.Id)
            .ToList();

        if (primesTransport.Count == 0)
            return 0;

        var primeReferenceId = primesTransport.Min();
        var stagiaireIds = ObtenirEmployesStagiaires(db);
        var modifs = 0;

        var affectations = db.AffectationsPrimesIndemnites
            .Where(a => primesTransport.Contains(a.PrimeIndemniteId))
            .ToList();

        foreach (var aff in affectations.ToList())
        {
            if (stagiaireIds.Contains(aff.EmployeId))
            {
                db.AffectationsPrimesIndemnites.Remove(aff);
                modifs++;
                continue;
            }

            if (aff.Montant != cible)
            {
                aff.Montant = cible;
                modifs++;
            }
        }

        var employesAvecTransport = db.AffectationsPrimesIndemnites
            .Where(a => primesTransport.Contains(a.PrimeIndemniteId))
            .Select(a => a.EmployeId)
            .ToHashSet();

        var employeIds = db.Employes.Select(e => e.Id).ToList();

        foreach (var employeId in employeIds)
        {
            if (stagiaireIds.Contains(employeId) || employesAvecTransport.Contains(employeId))
                continue;

            db.AffectationsPrimesIndemnites.Add(new AffectationPrimeIndemnite
            {
                EmployeId = employeId,
                PrimeIndemniteId = primeReferenceId,
                Montant = cible
            });
            modifs++;
        }

        if (modifs > 0)
            db.SaveChanges();

        return modifs;
    }

    private static HashSet<int> ObtenirEmployesStagiaires(PaieDbContext db)
        => db.Contrats
            .AsNoTracking()
            .Select(c => new { c.EmployeId, c.TypeContrat })
            .AsEnumerable()
            .Where(c => StagiairePaieHelper.EstStagiaire(c.TypeContrat))
            .Select(c => c.EmployeId)
            .ToHashSet();
}
