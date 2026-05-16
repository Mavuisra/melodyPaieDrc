using System.Globalization;
using System.Text.RegularExpressions;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Découpe les horodatages bruts en « moments » alignés sur <see cref="LtServicesPointageCalcul"/> :
/// lun–ven : entrée, début pause, fin pause, sortie (les 4 premiers horaires) ;
/// samedi : entrée = premier, sortie = dernier ;
/// dimanche : tout est hors journée type.
/// </summary>
public static class PointagesMomentsHelper
{
    public sealed record MomentsDecoupes(
        DateTime? Entree,
        DateTime? DebutPause,
        DateTime? FinPause,
        DateTime? Sortie,
        IReadOnlyList<DateTime> PointagesSupplementaires);

    public static MomentsDecoupes Decouper(IReadOnlyList<DateTime> pointages, DateTime jour)
    {
        var sorted = pointages.OrderBy(x => x).ToList();
        var dow = jour.DayOfWeek;

        if (dow == DayOfWeek.Sunday)
            return new MomentsDecoupes(null, null, null, null, sorted);

        if (dow == DayOfWeek.Saturday)
        {
            if (sorted.Count == 0)
                return new MomentsDecoupes(null, null, null, null, Array.Empty<DateTime>());
            if (sorted.Count == 1)
                return new MomentsDecoupes(sorted[0], null, null, null, Array.Empty<DateTime>());
            var entree = sorted[0];
            var sortie = sorted[^1];
            var extras = sorted.Count > 2 ? sorted.Skip(1).Take(sorted.Count - 2).ToList() : new List<DateTime>();
            return new MomentsDecoupes(entree, null, null, sortie, extras);
        }

        DateTime? e = sorted.Count > 0 ? sorted[0] : null;
        DateTime? dp = sorted.Count > 1 ? sorted[1] : null;
        DateTime? fp = sorted.Count > 2 ? sorted[2] : null;
        DateTime? s = sorted.Count > 3 ? sorted[3] : null;
        var sup = sorted.Count > 4 ? sorted.Skip(4).ToList() : new List<DateTime>();
        return new MomentsDecoupes(e, dp, fp, s, sup);
    }

    public static string FormaterHhMm(DateTime? dt)
    {
        if (!dt.HasValue) return "";
        return dt.Value.ToString("HH:mm", CultureInfo.CurrentCulture);
    }

    /// <summary>Interprète une saisie type 14:25, 14h25, 9:05 pour le jour donné.</summary>
    public static bool TryParseHeureDuJour(string? text, DateTime jour, out DateTime instant)
    {
        instant = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var t = text.Trim();

        if (TimeSpan.TryParse(t, CultureInfo.InvariantCulture, out var tsInv))
        {
            instant = jour.Date + NormalizeTs(tsInv);
            return true;
        }

        if (TimeSpan.TryParse(t, CultureInfo.CurrentCulture, out var tsCur))
        {
            instant = jour.Date + NormalizeTs(tsCur);
            return true;
        }

        var m = Regex.Match(t, @"^\s*(\d{1,2})\s*[hH:]\s*(\d{2})\s*$");
        if (m.Success &&
            int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h) &&
            int.TryParse(m.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var min) &&
            h is >= 0 and <= 23 && min is >= 0 and <= 59)
        {
            instant = jour.Date.Add(new TimeSpan(h, min, 0));
            return true;
        }

        return false;
    }

    private static TimeSpan NormalizeTs(TimeSpan ts)
    {
        if (ts.TotalHours >= 24)
            return TimeSpan.FromHours(ts.TotalHours % 24);
        return ts;
    }
}
