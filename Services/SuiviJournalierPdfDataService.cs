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
        var reglesLt = LtServicesReglesProvider.ChargerDepuisDb(db);
        var dateDebut = new DateTime(annee, mois, 1).Date;
        var dateFin = dateDebut.AddMonths(1).AddDays(-1).Date;

        var existants = db.SuivisJournaliers
            .AsNoTracking()
            .Where(s => s.EmployeId == employeId && s.Date >= dateDebut && s.Date <= dateFin)
            .ToDictionary(s => s.Date.Date);

        var result = new List<SuiviJournalierPdfLigne>();
        for (var d = dateDebut; d <= dateFin; d = d.AddDays(1))
        {
            existants.TryGetValue(d, out var existant);
            var typeJour = NormaliserTypeJour(existant?.TypeJour);
            decimal heures;
            string modeLibelle;

            if (typeJour == SuiviJournalier.TypeNormal && existant != null &&
                !string.IsNullOrEmpty(existant.PointagesJson) && !existant.HeuresManuelles)
            {
                heures = PointagesJournalierSerializer.CalculerHeuresLt(existant.PointagesJson, d, reglesLt);
            }
            else if (existant != null)
            {
                heures = existant.HeuresPrestees;
            }
            else
            {
                // Sans pointage ni saisie admin, l'export ne pré-remplit plus d'heures.
                heures = 0m;
            }

            if (existant == null)
                modeLibelle = "—";
            else if (!string.IsNullOrEmpty(existant.PointagesJson) && !existant.HeuresManuelles)
                modeLibelle = "Auto (LT)";
            else if (existant.HeuresManuelles)
                modeLibelle = "Manuel";
            else
                modeLibelle = "—";

            var jourCode = typeJour == SuiviJournalier.TypeNormal && heures > 0m ? 1 : 0;

            result.Add(new SuiviJournalierPdfLigne(
                d.ToString("dd/MM/yyyy", Fr),
                d.ToString("dddd", Fr),
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
        return ObtenirLignesPourEmploye(db, employeId, mois, annee).Sum(l => l.HeuresPrestees);
    }

    /// <summary>Lignes du mois indexées par date (à minuit).</summary>
    public static IReadOnlyDictionary<DateTime, SuiviJournalierPdfLigne> ObtenirLignesParDate(PaieDbContext db, int employeId, int mois, int annee)
    {
        var lignes = ObtenirLignesPourEmploye(db, employeId, mois, annee);
        var debut = new DateTime(annee, mois, 1).Date;
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
