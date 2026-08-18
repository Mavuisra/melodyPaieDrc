using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Helpers;

/// <summary>Ancienneté et droits à congé annuel (1,5 jour par mois d'ancienneté, mini. 12 jours/an).</summary>
public static class AncienneteCongeHelper
{
    public static DateTime? ResoudreDateEmbauche(IEnumerable<Contrat> contrats)
    {
        var dates = contrats
            .Select(c => c.DateDebut)
            .Where(d => d != default)
            .ToList();
        return dates.Count == 0 ? null : dates.Min();
    }

    public static (int Annees, int Mois, int Jours) CalculerAnciennete(DateTime dateEmbauche, DateTime? au = null)
    {
        var fin = (au ?? DateTime.Today).Date;
        var debut = dateEmbauche.Date;
        if (fin < debut)
            return (0, 0, 0);

        var annees = fin.Year - debut.Year;
        var mois = fin.Month - debut.Month;
        var jours = fin.Day - debut.Day;
        if (jours < 0)
        {
            mois--;
            jours += DateTime.DaysInMonth(fin.Year, fin.Month == 1 ? 12 : fin.Month - 1);
        }
        if (mois < 0)
        {
            annees--;
            mois += 12;
        }
        return (Math.Max(0, annees), Math.Max(0, mois), Math.Max(0, jours));
    }

    public static string FormaterAnciennete(DateTime dateEmbauche, DateTime? au = null)
    {
        var (a, m, j) = CalculerAnciennete(dateEmbauche, au);
        return $"{a} an(s), {m} mois, {j} jour(s)";
    }

    /// <summary>1,5 jour ouvrable par mois d'ancienneté, plancher 12 jours après 12 mois.</summary>
    public static decimal CalculerJoursCongesAnnuels(DateTime dateEmbauche, DateTime? au = null)
    {
        var (annees, mois, _) = CalculerAnciennete(dateEmbauche, au);
        var moisTotal = annees * 12 + mois;
        if (moisTotal <= 0)
            return 0m;
        var jours = Math.Round(moisTotal * 1.5m, 1, MidpointRounding.AwayFromZero);
        if (moisTotal >= 12)
            jours = Math.Max(jours, 12m);
        return jours;
    }
}
