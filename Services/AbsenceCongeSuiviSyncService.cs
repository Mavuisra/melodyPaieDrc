using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Propage les périodes d'absence/congé vers le suivi journalier (types de jour + heures nominales).
/// Ne remplace pas un jour déjà pointé manuellement ou avec horodatages terminal.
/// </summary>
public static class AbsenceCongeSuiviSyncService
{
    public static void SynchroniserAbsence(PaieDbContext db, AbsenceConge absence)
    {
        var debut = absence.DateDebut.Date;
        var fin = absence.DateFin.Date;
        if (fin < debut)
            return;

        for (var d = debut; d <= fin; d = d.AddDays(1))
            AppliquerJour(db, absence.EmployeId, d, absence, persister: false);

        db.SaveChanges();
    }

    public static void RetirerAbsence(PaieDbContext db, AbsenceConge absence)
    {
        var debut = absence.DateDebut.Date;
        var fin = absence.DateFin.Date;
        if (fin < debut)
            return;

        var typeAttendu = MapperTypeJour(absence);

        for (var d = debut; d <= fin; d = d.AddDays(1))
        {
            var autre = TrouverAbsenceCouvrantJour(db, absence.EmployeId, d, absence.Id);
            if (autre != null)
            {
                AppliquerJour(db, absence.EmployeId, d, autre, persister: false);
                continue;
            }

            var suivi = db.SuivisJournaliers
                .FirstOrDefault(s => s.EmployeId == absence.EmployeId && s.Date == d);
            if (suivi == null || !PeutEcraser(suivi))
                continue;

            if (!string.Equals(suivi.TypeJour, typeAttendu, StringComparison.OrdinalIgnoreCase))
                continue;

            db.SuivisJournaliers.Remove(suivi);
        }

        db.SaveChanges();
    }

    private static AbsenceConge? TrouverAbsenceCouvrantJour(PaieDbContext db, int employeId, DateTime jour, int exclureId)
    {
        return db.AbsencesConges
            .AsNoTracking()
            .Where(a => a.EmployeId == employeId
                        && a.Id != exclureId
                        && a.DateDebut.Date <= jour
                        && a.DateFin.Date >= jour)
            .OrderByDescending(a => a.DateDebut)
            .ThenByDescending(a => a.Id)
            .FirstOrDefault();
    }

    private static void AppliquerJour(PaieDbContext db, int employeId, DateTime jour, AbsenceConge absence, bool persister = true)
    {
        var typeJour = MapperTypeJour(absence);
        var suivi = db.SuivisJournaliers
            .FirstOrDefault(s => s.EmployeId == employeId && s.Date == jour);

        if (suivi != null && !PeutEcraser(suivi))
            return;

        var heures = CalculerHeuresPourType(db, jour, typeJour);

        if (suivi != null)
        {
            suivi.TypeJour = typeJour;
            suivi.HeuresPrestees = heures;
            suivi.PointagesJson = null;
            suivi.HeuresManuelles = false;
        }
        else
        {
            db.SuivisJournaliers.Add(new SuiviJournalier
            {
                EmployeId = employeId,
                Date = jour,
                TypeJour = typeJour,
                HeuresPrestees = heures,
                PointagesJson = null,
                HeuresManuelles = false
            });
        }

        if (persister)
            db.SaveChanges();
    }

    private static bool PeutEcraser(SuiviJournalier suivi)
    {
        if (!string.IsNullOrWhiteSpace(suivi.PointagesJson))
            return false;
        if (suivi.HeuresManuelles)
            return false;
        if (string.Equals(suivi.TypeJour, SuiviJournalier.TypeNormal, StringComparison.OrdinalIgnoreCase)
            && suivi.HeuresPrestees > 0m)
            return false;
        return true;
    }

    private static decimal CalculerHeuresPourType(PaieDbContext db, DateTime jour, string typeJour)
    {
        if (string.Equals(typeJour, SuiviJournalier.TypePreavis, StringComparison.OrdinalIgnoreCase)
            || string.Equals(typeJour, SuiviJournalier.TypeAbsence, StringComparison.OrdinalIgnoreCase))
            return 0m;

        if (SuiviJournalier.EstTypeJourSpecialPaye(typeJour))
            return SuiviJournalierCalculPaieHelper.DeterminerHeuresNominalesJourDepuisDb(db, jour);

        return 0m;
    }

    internal static string MapperTypeJour(AbsenceConge absence)
    {
        if (!absence.EstPaye)
            return SuiviJournalier.TypeAbsence;

        return absence.Type.Trim() switch
        {
            "Congé annuel" => SuiviJournalier.TypeCongeAnnuel,
            "Congé circonstanciel" => SuiviJournalier.TypeCongeCirconstance,
            "Maladie" or "Maternité" => SuiviJournalier.TypeMaladie,
            "Mission" => SuiviJournalier.TypeCongeCirconstance,
            _ => SuiviJournalier.TypeCongeCirconstance
        };
    }
}
