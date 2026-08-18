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
}
