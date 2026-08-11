using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Construit les lignes d’export PDF pointage pour un employé / période (même logique que la grille à l’écran).
/// </summary>
public static class SuiviJournalierPdfDataService
{
    private static readonly CultureInfo Fr = new("fr-FR");

    /// <summary>Grille complète du mois pour l’employé (état calculé depuis la base).</summary>
    public static IReadOnlyList<SuiviJournalierPdfLigne> ObtenirLignesPourEmploye(PaieDbContext db, int employeId, int mois, int annee)
    {
        var periode = new PeriodePaie { Mois = mois, Annee = annee };
        var (politique, dateDebut, dateFin) = PeriodePaieHelper.ResoudrePeriode(db, periode);
        var reglesLt = LtServicesReglesProvider.ChargerDepuisDb(db);

        var existantsList = db.SuivisJournaliers
            .AsNoTracking()
            .Where(s => s.EmployeId == employeId && s.Date >= dateDebut && s.Date <= dateFin)
            .ToList();
        var existants = existantsList.ToDictionary(s => s.Date.Date);

        var calendrierCtx = SuiviJournalierCalculPaieHelper.ChargerCalendrierPaie(db, dateDebut, dateFin);
        var semaineSixJours = calendrierCtx.SemaineSixJours || politique.ForcerSamediOuvre;
        var fusionnes = SuiviJournalierGrilleHelper.FusionnerMoisCompletPourCalculPaie(
            employeId,
            dateDebut,
            dateFin,
            existantsList,
            semaineSixJours,
            calendrierCtx.Calendrier,
            politique.CompleterJoursSansSaisie,
            politique.ForcerSamediOuvre);

        var result = new List<SuiviJournalierPdfLigne>();
        foreach (var s in fusionnes)
        {
            existants.TryGetValue(s.Date.Date, out var existantDb);
            var typeJour = NormaliserTypeJour(existantDb?.TypeJour ?? s.TypeJour);
            decimal heures;
            string modeLibelle;

            if (typeJour == SuiviJournalier.TypeNormal && existantDb != null)
            {
                heures = SuiviJournalierCalculPaieHelper.RecalculerHeuresEffectives(existantDb, reglesLt);
            }
            else if (existantDb != null)
            {
                heures = existantDb.HeuresPrestees;
            }
            else
            {
                heures = s.HeuresPrestees;
            }

            if (existantDb == null)
                modeLibelle = heures > 0m ? "Défaut (politique)" : "—";
            else if (!string.IsNullOrEmpty(existantDb.PointagesJson) && !existantDb.HeuresManuelles)
                modeLibelle = "Auto (LT)";
            else if (existantDb.HeuresManuelles)
                modeLibelle = "Manuel";
            else
                modeLibelle = heures > 0m ? "Saisie" : "—";

            var jourCode = typeJour == SuiviJournalier.TypeNormal && heures > 0m ? 1 : 0;

            result.Add(new SuiviJournalierPdfLigne(
                s.Date.ToString("dd/MM/yyyy", Fr),
                s.Date.ToString("dddd", Fr),
                jourCode,
                modeLibelle,
                heures,
                typeJour));
        }

        return result;
    }

    /// <summary>Somme des heures prestées sur la période (même règles que la grille / export PDF / base paie).</summary>
    public static decimal CalculerTotalHeuresPourEmploye(PaieDbContext db, int employeId, int mois, int annee)
    {
        var periode = new PeriodePaie { Mois = mois, Annee = annee };
        return SuiviJournalierCalculPaieHelper.CalculerTotauxPresenceEmploye(db, employeId, periode).TotalHeures;
    }

    /// <summary>Jours équivalents pondérés (8 h / 5 h sam.) — même formule que le calcul de paie.</summary>
    public static decimal CalculerJoursEquivalentsPourEmploye(PaieDbContext db, int employeId, int mois, int annee)
    {
        var periode = new PeriodePaie { Mois = mois, Annee = annee };
        return SuiviJournalierCalculPaieHelper.CalculerTotauxPresenceEmploye(db, employeId, periode).JoursEquivalents;
    }

    /// <summary>Lignes du mois indexées par date (à minuit).</summary>
    public static IReadOnlyDictionary<DateTime, SuiviJournalierPdfLigne> ObtenirLignesParDate(PaieDbContext db, int employeId, int mois, int annee)
    {
        var lignes = ObtenirLignesPourEmploye(db, employeId, mois, annee);
        var periode = new PeriodePaie { Mois = mois, Annee = annee };
        var (_, debut, _) = PeriodePaieHelper.ResoudrePeriode(db, periode);
        var dict = new Dictionary<DateTime, SuiviJournalierPdfLigne>();
        for (var i = 0; i < lignes.Count; i++)
            dict[debut.AddDays(i)] = lignes[i];
        return dict;
    }

    private static string NormaliserTypeJour(string? typeJour)
    {
        if (string.IsNullOrWhiteSpace(typeJour))
            return SuiviJournalier.TypeNormal;

        return typeJour.Trim() switch
        {
            SuiviJournalier.TypeNormal => SuiviJournalier.TypeNormal,
            SuiviJournalier.TypeCongeAnnuel => SuiviJournalier.TypeCongeAnnuel,
            SuiviJournalier.TypeCongeCirconstance => SuiviJournalier.TypeCongeCirconstance,
            SuiviJournalier.TypeMaladie => SuiviJournalier.TypeMaladie,
            SuiviJournalier.TypePreavis => SuiviJournalier.TypePreavis,
            "Absence justifiée" => SuiviJournalier.TypeCongeCirconstance,
            "Absence non justifiée" => SuiviJournalier.TypeNormal,
            "Malade" => SuiviJournalier.TypeMaladie,
            _ => SuiviJournalier.TypeNormal
        };
    }
}
