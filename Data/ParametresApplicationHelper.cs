using System;
using System.Linq;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Data;

/// <summary>
/// Taux de change global (CDF/USD) — modifiable à tout moment ; utilisé pour les calculs de bulletins.
/// </summary>
public static class ParametresApplicationHelper
{
    public const decimal TauxParDefaut = 3000m;

    /// <summary>Assure la présence de la ligne Id = 1.</summary>
    public static void EnsureRow(PaieDbContext db)
    {
        if (db.ParametresApplication.AsNoTracking().Any(p => p.Id == ParametresApplication.SingletonId))
            return;

        db.ParametresApplication.Add(new ParametresApplication
        {
            Id = ParametresApplication.SingletonId,
            TauxCdfParUsd = TauxParDefaut,
            DateDerniereModification = DateTime.UtcNow,
            ZkTerminalPort = 4370,
            ZkMachineNumber = 1,
            ZkCommPassword = 0,
            ZkSyncActif = false,
            ZkIntervalleSecondes = 60,
            LtHeureDebutTravail = "07:30",
            LtHeureLimiteTolerance = "07:40",
            LtHeureDebutPause = "12:00",
            LtHeureFinPause = "13:00",
            LtHeureFinSemaine = "16:00",
            LtHeureFinSamedi = "12:30"
        });
        db.SaveChanges();
    }

    /// <summary>Taux CDF pour 1 USD (&gt; 0).</summary>
    public static decimal GetTauxCdfParUsd(PaieDbContext db)
    {
        EnsureRow(db);
        var p = db.ParametresApplication.Find(ParametresApplication.SingletonId);
        if (p != null && p.TauxCdfParUsd > 0)
            return p.TauxCdfParUsd;
        return TauxParDefaut;
    }

    /// <summary>Enregistre le taux et optionnellement aligne les périodes de paie encore ouvertes.</summary>
    public static void SetTauxCdfParUsd(PaieDbContext db, decimal taux, bool mettreAJourPeriodesNonCloturees)
    {
        if (taux <= 0)
            throw new ArgumentOutOfRangeException(nameof(taux), "Le taux doit être supérieur à 0.");

        EnsureRow(db);
        var p = db.ParametresApplication.Find(ParametresApplication.SingletonId)
                ?? throw new InvalidOperationException("Paramètres application introuvables.");

        p.TauxCdfParUsd = decimal.Round(taux, 4, MidpointRounding.AwayFromZero);
        p.DateDerniereModification = DateTime.UtcNow;

        if (mettreAJourPeriodesNonCloturees)
        {
            foreach (var per in db.PeriodesPaie.Where(x => !x.Cloturee))
                per.TauxChangeBudget = p.TauxCdfParUsd;
        }

        db.SaveChanges();
    }
}
