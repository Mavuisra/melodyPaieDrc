using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

public static class QuinzaineOctroiService
{
    public static void SynchroniserAcomptesPeriode(PaieDbContext db, int employeId, int periodePaieId)
    {
        var total = db.QuinzaineOctrois
            .Where(q => q.EmployeId == employeId && q.PeriodePaieId == periodePaieId)
            .Select(q => q.Montant)
            .ToList()
            .Sum();

        var saisie = db.SaisiesPaie.FirstOrDefault(s => s.EmployeId == employeId && s.PeriodePaieId == periodePaieId);
        if (saisie == null)
        {
            if (total <= 0)
                return;

            saisie = new SaisiePaie
            {
                EmployeId = employeId,
                PeriodePaieId = periodePaieId,
                AcomptesSalaire = total
            };
            db.SaisiesPaie.Add(saisie);
        }
        else
            saisie.AcomptesSalaire = total;
    }

    /// <summary>Synchronise les acomptes (quinzaines) pour tous les employés ayant un octroi sur la période.</summary>
    public static void SynchroniserAcomptesPeriodePourTous(PaieDbContext db, int periodePaieId)
    {
        var employeIds = db.QuinzaineOctrois
            .Where(q => q.PeriodePaieId == periodePaieId)
            .Select(q => q.EmployeId)
            .Distinct()
            .ToList();

        foreach (var employeId in employeIds)
            SynchroniserAcomptesPeriode(db, employeId, periodePaieId);
    }
}
