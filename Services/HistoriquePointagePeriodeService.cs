using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>Construit l'historique des pointages d'une période (même source que le live : SuivisJournaliers.PointagesJson).</summary>
public static class HistoriquePointagePeriodeService
{
    public static IReadOnlyList<PresenceEmployeSyntheseLigne> Charger(
        PaieDbContext db,
        PeriodePaie periode,
        int? employeIdFiltre,
        string? recherche)
    {
        var (_, dateDebut, dateFin) = PeriodePaieHelper.ResoudrePeriode(db, periode);
        var reglesLt = LtServicesReglesProvider.ChargerDepuisDb(db);

        var employesQuery = db.Employes.AsNoTracking().Include(e => e.Departement).AsQueryable();
        if (employeIdFiltre is int idFiltre && idFiltre > 0)
            employesQuery = employesQuery.Where(e => e.Id == idFiltre);
        var employes = employesQuery.ToList();
        if (!string.IsNullOrWhiteSpace(recherche))
        {
            var q = recherche.Trim();
            employes = employes.Where(e =>
                (e.Matricule ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                || (e.Nom ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                || (e.Postnom ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
                || (e.Prenom ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var employeIds = employes.Select(e => e.Id).ToList();
        var suivis = db.SuivisJournaliers.AsNoTracking()
            .Where(s => s.Date >= dateDebut && s.Date <= dateFin && employeIds.Contains(s.EmployeId))
            .ToList();
        var parEmployeJour = suivis
            .GroupBy(s => (s.EmployeId, s.Date.Date))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

        var lignes = new List<PresenceEmployeSyntheseLigne>();
        var unSeulEmploye = employeIdFiltre is > 0 && employes.Count == 1;

        foreach (var emp in employes.OrderBy(e => e.Nom).ThenBy(e => e.Prenom))
        {
            if (unSeulEmploye)
            {
                for (var d = dateDebut.Date; d <= dateFin.Date; d = d.AddDays(1))
                    lignes.Add(ConstruireLigne(emp, d, parEmployeJour, reglesLt));
            }
            else
            {
                var jours = parEmployeJour.Keys.Where(k => k.EmployeId == emp.Id).Select(k => k.Date).OrderBy(x => x);
                foreach (var d in jours)
                    lignes.Add(ConstruireLigne(emp, d, parEmployeJour, reglesLt));
            }
        }

        return lignes;
    }

    private static PresenceEmployeSyntheseLigne ConstruireLigne(
        Employe emp,
        DateTime jour,
        Dictionary<(int EmployeId, DateTime Date), SuiviJournalier> parEmployeJour,
        LtServicesRegles reglesLt)
    {
        parEmployeJour.TryGetValue((emp.Id, jour.Date), out var sj);
        var nom = $"{emp.Nom} {emp.Postnom} {emp.Prenom}".Trim();
        var ligne = new PresenceEmployeSyntheseLigne
        {
            EmployeId = emp.Id,
            DateJour = jour.Date,
            Jour = jour.ToString("dd/MM/yyyy"),
            Matricule = string.IsNullOrWhiteSpace(emp.Matricule) ? "—" : emp.Matricule,
            NomComplet = nom,
            Departement = emp.Departement?.NomDepartement ?? "—"
        };

        if (sj == null)
        {
            ligne.Statut = "Aucune donnée";
            ligne.AbsenceLibelle = "—";
            ligne.IndicateurRetard = "—";
            ligne.AucuneDonnee = true;
            return ligne;
        }

        var typeJour = string.IsNullOrWhiteSpace(sj.TypeJour) ? SuiviJournalier.TypeNormal : sj.TypeJour.Trim();
        var pointages = PointagesJournalierSerializer.Deserialiser(sj.PointagesJson, jour);
        var reglesEmp = RetardPaieHelper.ReglesPourEmploye(reglesLt, emp);

        if (!string.Equals(typeJour, SuiviJournalier.TypeNormal, StringComparison.OrdinalIgnoreCase))
        {
            ligne.Statut = typeJour;
            ligne.AbsenceLibelle = typeJour;
            ligne.IndicateurRetard = "—";
            if (string.Equals(typeJour, SuiviJournalier.TypeAbsence, StringComparison.OrdinalIgnoreCase))
                ligne.AbsenceLibelle = "Absence";
            return ligne;
        }

        if (pointages.Count == 0)
        {
            ligne.Statut = sj.HeuresPrestees <= 0 ? "Absent" : "Heures manuelles";
            ligne.AbsenceLibelle = sj.HeuresPrestees <= 0 ? "Absence" : "—";
            ligne.IndicateurRetard = "—";
            return ligne;
        }

        var decoupe = PointagesMomentsHelper.Decouper(pointages, jour, reglesEmp);
        static string HeureMin(DateTime? dt) => dt.HasValue ? dt.Value.ToString("HH:mm") : "—";
        var estRetard = RetardPaieHelper.EstRetard(decoupe.Entree, reglesEmp.HeureLimiteTolerance);
        var minutesRetard = decoupe.Entree.HasValue
            ? RetardPaieHelper.CalculerMinutesRetard(decoupe.Entree.Value, reglesEmp.HeureLimiteTolerance)
            : 0;

        ligne.Entree = HeureMin(decoupe.Entree);
        ligne.DebutPause = HeureMin(decoupe.DebutPause);
        ligne.FinPause = HeureMin(decoupe.FinPause);
        ligne.Sortie = HeureMin(decoupe.Sortie);
        ligne.EstRetard = estRetard;
        ligne.IndicateurRetard = estRetard ? "En retard" : "À l'heure";
        ligne.MinutesRetard = minutesRetard;
        ligne.Statut = estRetard ? "Présent (retard)" : "Présent";
        ligne.AbsenceLibelle = "—";
        ligne.HeureLimiteLibelle = RetardPaieHelper.LibelleHeureLimite(reglesEmp);
        return ligne;
    }
}
