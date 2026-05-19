using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>Recalcule les heures prestées à partir des pointages enregistrés (après changement de règles LT).</summary>
public static class SuiviJournalierRecalculService
{
    public static int RecalculerAutomatiqueEntrepriseCourante(PaieDbContext db)
    {
        var regles = LtServicesReglesProvider.ChargerDepuisDb(db);
        var employeIds = ContexteEntrepriseService.EmployesEntrepriseCourante(db)
            .AsNoTracking()
            .Select(e => e.Id)
            .ToList();

        if (employeIds.Count == 0)
            return 0;

        var suivis = db.SuivisJournaliers
            .Where(s => employeIds.Contains(s.EmployeId)
                        && !s.HeuresManuelles
                        && s.PointagesJson != null
                        && s.PointagesJson != ""
                        && s.PointagesJson != "[]")
            .ToList();

        var nb = 0;
        foreach (var suivi in suivis)
        {
            suivi.HeuresPrestees = PointagesJournalierSerializer.CalculerHeuresLt(suivi.PointagesJson, suivi.Date, regles);
            nb++;
        }

        if (nb > 0)
            db.SaveChanges();

        return nb;
    }
}
