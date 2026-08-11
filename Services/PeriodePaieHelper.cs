using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Bornes de période de paie selon la politique (mois calendaire ou fenêtre décalée ex. 26→25).
/// </summary>
public static class PeriodePaieHelper
{
    public static (DateTime Debut, DateTime Fin) ObtenirBornes(PeriodePaie periode, PolitiquePaieContext politique)
    {
        if (!politique.PeriodeDecalee)
        {
            var debut = new DateTime(periode.Annee, periode.Mois, 1);
            return (debut, debut.AddMonths(1).AddDays(-1));
        }

        var jourFin = Math.Clamp((int)politique.JourFinPeriodeDecalee, 1, 28);
        var jourDebut = Math.Clamp((int)politique.JourDebutPeriodeDecalee, 1, 28);

        var moisFin = periode.Mois;
        var anneeFin = periode.Annee;
        var fin = SafeDate(anneeFin, moisFin, jourFin);

        var moisDebut = moisFin == 1 ? 12 : moisFin - 1;
        var anneeDebut = moisFin == 1 ? anneeFin - 1 : anneeFin;
        var debutDecale = SafeDate(anneeDebut, moisDebut, jourDebut);

        return (debutDecale, fin);
    }

    /// <summary>Fin effective pour un calcul en cours (n'inclut pas les jours futurs).</summary>
    public static DateTime ObtenirFinCalcul(PeriodePaie periode, PolitiquePaieContext politique, DateTime aujourdhui)
    {
        var (debut, fin) = ObtenirBornes(periode, politique);
        aujourdhui = aujourdhui.Date;
        if (!periode.Cloturee && aujourdhui >= debut && aujourdhui <= fin)
            return aujourdhui;
        return fin;
    }

    public static bool ContientDate(PeriodePaie periode, PolitiquePaieContext politique, DateTime date)
    {
        var (debut, fin) = ObtenirBornes(periode, politique);
        date = date.Date;
        return date >= debut && date <= fin;
    }

    public static string LibellePeriode(PeriodePaie periode, PolitiquePaieContext politique)
    {
        if (!politique.PeriodeDecalee)
            return $"{periode.Mois:D2}/{periode.Annee}";

        var (debut, fin) = ObtenirBornes(periode, politique);
        return $"{debut:dd/MM/yyyy} → {fin:dd/MM/yyyy}";
    }

    /// <summary>Charge la politique active et retourne les bornes de la période.</summary>
    public static (PolitiquePaieContext Politique, DateTime Debut, DateTime Fin) ResoudrePeriode(
        PaieDbContext db,
        PeriodePaie periode,
        int? entrepriseId = null)
    {
        var eid = entrepriseId ?? periode.EntrepriseId ?? ContexteEntrepriseService.ObtenirEntrepriseCouranteId(db);
        var politique = new PolitiquePaieService(db).Charger(eid);
        var (debut, fin) = ObtenirBornes(periode, politique);
        return (politique, debut, fin);
    }

    private static DateTime SafeDate(int annee, int mois, int jour)
    {
        jour = Math.Min(jour, DateTime.DaysInMonth(annee, mois));
        return new DateTime(annee, mois, jour);
    }
}
