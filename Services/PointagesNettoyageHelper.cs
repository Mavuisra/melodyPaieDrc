using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Filtre les lectures accidentelles (double-appui terminal) et sélectionne les horaires
/// retenus pour le calcul selon des plages horaires, et non plus uniquement par rang dans la liste.
/// </summary>
public static class PointagesNettoyageHelper
{
    /// <summary>Écart minimal entre deux lectures distinctes (anti double-appui).</summary>
    public static readonly TimeSpan IntervalleAntiDoublon = TimeSpan.FromMinutes(3);

    /// <summary>Écart trop court entre entrée et sortie pour être une vraie journée (erreur de double pointage).</summary>
    public static readonly TimeSpan DureeMinEntreeSortie = TimeSpan.FromMinutes(20);

    /// <summary>Supprime les lectures consécutives trop rapprochées (garde la première de chaque groupe).</summary>
    public static IReadOnlyList<DateTime> FiltrerDoublonsAccidentels(
        IReadOnlyList<DateTime> pointages,
        TimeSpan? intervalleMin = null)
    {
        var min = intervalleMin ?? IntervalleAntiDoublon;
        var sorted = pointages.OrderBy(t => t).ToList();
        if (sorted.Count <= 1)
            return sorted;

        var result = new List<DateTime> { sorted[0] };
        for (var i = 1; i < sorted.Count; i++)
        {
            if (sorted[i] - result[^1] >= min)
                result.Add(sorted[i]);
        }

        return result;
    }

    /// <summary>Indique si une lecture est ignorée car doublon accidentel par rapport aux lectures retenues.</summary>
    public static bool EstLectureIgnoree(DateTime lecture, IReadOnlyList<DateTime> retenuesPourCalcul) =>
        !retenuesPourCalcul.Contains(lecture);

    /// <summary>Horodatages retenus pour le calcul LT (dédoublonnage + plages horaires).</summary>
    public static IReadOnlyList<DateTime> SelectionnerPourCalcul(
        IReadOnlyList<DateTime> pointages,
        DateTime jour,
        LtServicesRegles? regles = null)
    {
        var r = regles ?? LtServicesRegles.Defaut;
        if (pointages == null || pointages.Count == 0)
            return Array.Empty<DateTime>();

        var debounced = FiltrerDoublonsAccidentels(pointages).ToList();
        if (debounced.Count == 0)
            return debounced;

        var dow = jour.DayOfWeek;
        if (dow == DayOfWeek.Sunday)
            return debounced;

        if (dow == DayOfWeek.Saturday)
            return SelectionnerSamedi(debounced, r);

        if (r.UtiliseDeuxPointages)
            return SelectionnerDeuxPointages(debounced, r);

        if (r.UtiliseTroisPointages)
            return SelectionnerTroisPointages(debounced, r);

        return SelectionnerQuatrePointages(debounced, r);
    }

    private static List<DateTime> SelectionnerSamedi(List<DateTime> debounced, LtServicesRegles r)
    {
        if (debounced.Count <= 1)
            return debounced;

        var entree = debounced[0];
        var sortie = debounced[^1];
        if (debounced.Count == 2 && sortie - entree < DureeMinEntreeSortie)
            return new List<DateTime> { entree };

        return new List<DateTime> { entree, sortie };
    }

    private static List<DateTime> SelectionnerDeuxPointages(List<DateTime> debounced, LtServicesRegles r)
    {
        if (debounced.Count == 1)
            return debounced;

        var matin = debounced.Where(t => t.TimeOfDay < r.HeureDebutPause).ToList();
        var apresMidi = debounced.Where(t => t.TimeOfDay >= r.HeureDebutPause).ToList();

        if (apresMidi.Count == 0)
            return new List<DateTime> { debounced[0] };

        if (matin.Count == 0)
        {
            if (debounced.Count == 1)
                return debounced;
            var seuleSortie = debounced[^1];
            if (debounced.Count >= 2 && debounced[^1] - debounced[0] < DureeMinEntreeSortie)
                return new List<DateTime> { debounced[0] };
            return new List<DateTime> { debounced[0], seuleSortie };
        }

        var entree = matin[0];
        var sortie = apresMidi[^1];
        if (sortie - entree < DureeMinEntreeSortie)
            return new List<DateTime> { entree };

        return new List<DateTime> { entree, sortie };
    }

    private static List<DateTime> SelectionnerTroisPointages(List<DateTime> debounced, LtServicesRegles r)
    {
        if (debounced.Count == 1)
            return debounced;

        if (debounced.Count == 2)
            return SelectionnerDeuxPointages(debounced, r);

        var entree = debounced.Where(t => t.TimeOfDay < r.HeureDebutPause).FirstOrDefault();
        if (entree == default)
            entree = debounced[0];

        var fenetrePause = FenetrePause(r);
        var enPause = debounced.Where(t => DansFenetre(t.TimeOfDay, fenetrePause)).ToList();
        var pause = enPause.Count > 0
            ? enPause[0]
            : debounced.FirstOrDefault(t => t > entree && t.TimeOfDay < r.HeureFinPause);
        if (pause == default)
            pause = debounced.Count > 1 ? debounced[1] : entree;

        var candidatsSortie = debounced.Where(t => t.TimeOfDay >= r.HeureFinPause && t > pause).ToList();
        var sortie = candidatsSortie.Count > 0
            ? candidatsSortie[^1]
            : debounced[^1];

        if (sortie <= entree)
            return new List<DateTime> { entree };

        if (pause <= entree)
            return SelectionnerDeuxPointages(debounced, r);

        if (sortie - entree < DureeMinEntreeSortie)
            return new List<DateTime> { entree };

        if (sortie <= pause)
            return new List<DateTime> { entree, sortie };

        return new List<DateTime> { entree, pause, sortie };
    }

    private static List<DateTime> SelectionnerQuatrePointages(List<DateTime> debounced, LtServicesRegles r)
    {
        if (debounced.Count <= 1)
            return debounced;

        if (debounced.Count == 2)
            return SelectionnerDeuxPointages(debounced, r);

        if (debounced.Count == 3)
            return SelectionnerTroisPointages(debounced, r);

        var entree = debounced.Where(t => t.TimeOfDay < r.HeureDebutPause).FirstOrDefault();
        if (entree == default)
            entree = debounced[0];

        var fenetrePause = FenetrePause(r);
        var enPause = debounced.Where(t => DansFenetre(t.TimeOfDay, fenetrePause)).ToList();

        DateTime debutPause;
        DateTime finPause;
        if (enPause.Count >= 2)
        {
            debutPause = enPause[0];
            finPause = enPause[^1];
        }
        else if (enPause.Count == 1)
        {
            debutPause = enPause[0];
            finPause = enPause[0];
        }
        else
        {
            var milieu = debounced.Where(t => t > entree && t.TimeOfDay < r.HeureFinPause).ToList();
            if (milieu.Count >= 2)
            {
                debutPause = milieu[0];
                finPause = milieu[^1];
            }
            else if (milieu.Count == 1)
            {
                debutPause = milieu[0];
                finPause = milieu[0];
            }
            else
                return SelectionnerTroisPointages(debounced, r);
        }

        var candidatsSortie = debounced.Where(t => t > finPause).ToList();
        var sortie = candidatsSortie.Count > 0 ? candidatsSortie[^1] : debounced[^1];

        if (sortie - entree < DureeMinEntreeSortie)
            return new List<DateTime> { entree };

        if (debutPause <= entree)
            debutPause = debounced.First(t => t > entree);

        if (finPause < debutPause)
            finPause = debutPause;

        if (sortie <= finPause)
            return new List<DateTime> { entree, debutPause, finPause };

        return new List<DateTime> { entree, debutPause, finPause, sortie };
    }

    private static (TimeSpan Debut, TimeSpan Fin) FenetrePause(LtServicesRegles r)
    {
        var marge = TimeSpan.FromMinutes(30);
        var debut = r.HeureDebutPause > marge ? r.HeureDebutPause - marge : TimeSpan.Zero;
        var fin = r.HeureFinPause + marge;
        if (fin.TotalHours >= 24)
            fin = new TimeSpan(23, 59, 0);
        return (debut, fin);
    }

    private static bool DansFenetre(TimeSpan heure, (TimeSpan Debut, TimeSpan Fin) fenetre) =>
        heure >= fenetre.Debut && heure <= fenetre.Fin;
}
