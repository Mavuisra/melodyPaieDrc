using System;
using System.Linq;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Data;

/// <summary>
/// Paramètres application : ligne globale (Id=1) + une ligne par entreprise (multi-tenant).
/// </summary>
public static class ParametresApplicationHelper
{
    public const decimal TauxParDefaut = 3000m;

    public static void EnsureRow(PaieDbContext db)
    {
        if (db.ParametresApplication.IgnoreQueryFilters().AsNoTracking()
            .Any(p => p.Id == ParametresApplication.SingletonId))
            return;

        db.ParametresApplication.Add(CreerParametresParDefaut(ParametresApplication.SingletonId, null));
        db.SaveChanges();
    }

    public static void EnsureRowForEntreprise(PaieDbContext db, int entrepriseId)
    {
        if (entrepriseId <= 0) return;
        EnsureRow(db);
        if (db.ParametresApplication.IgnoreQueryFilters().Any(p => p.EntrepriseId == entrepriseId))
            return;

        var global = db.ParametresApplication.IgnoreQueryFilters()
            .FirstOrDefault(p => p.Id == ParametresApplication.SingletonId);
        var row = CreerParametresParDefaut(0, entrepriseId);
        if (global != null)
        {
            row.TauxCdfParUsd = global.TauxCdfParUsd > 0 ? global.TauxCdfParUsd : TauxParDefaut;
            row.ZkTerminalIp = global.ZkTerminalIp;
            row.ZkTerminalPort = global.ZkTerminalPort;
            row.ZkMachineNumber = global.ZkMachineNumber;
            row.ZkCommPassword = global.ZkCommPassword;
            row.ZkSyncActif = global.ZkSyncActif;
            row.ZkIntervalleSecondes = global.ZkIntervalleSecondes;
            row.LtHeureDebutTravail = global.LtHeureDebutTravail;
            row.LtHeureLimiteTolerance = global.LtHeureLimiteTolerance;
            row.LtHeureDebutPause = global.LtHeureDebutPause;
            row.LtHeureFinPause = global.LtHeureFinPause;
            row.LtHeureFinSemaine = global.LtHeureFinSemaine;
            row.LtHeureFinSamedi = global.LtHeureFinSamedi;
            row.LtModePointage = global.LtModePointage;
            row.LtDeductionPauseAutomatique = global.LtDeductionPauseAutomatique;
        }

        db.ParametresApplication.Add(row);
        db.SaveChanges();
    }

    public static ParametresApplication GetParametresEntrepriseCourante(PaieDbContext db)
    {
        var entrepriseId = db.TenantId > 0
            ? db.TenantId
            : ContexteEntrepriseService.EntrepriseCouranteId ?? 0;

        if (entrepriseId <= 0)
        {
            EnsureRow(db);
            return db.ParametresApplication.Find(ParametresApplication.SingletonId)
                   ?? CreerParametresParDefaut(ParametresApplication.SingletonId, null);
        }

        EnsureRowForEntreprise(db, entrepriseId);
        return db.ParametresApplication.IgnoreQueryFilters()
            .First(p => p.EntrepriseId == entrepriseId);
    }

    public static ParametresApplication GetLigneGlobale(PaieDbContext db)
    {
        EnsureRow(db);
        return db.ParametresApplication.Find(ParametresApplication.SingletonId)
               ?? throw new InvalidOperationException("Paramètres globaux introuvables.");
    }

    public static int? GetDerniereEntrepriseActive(PaieDbContext db)
    {
        EnsureRow(db);
        return GetLigneGlobale(db).DerniereEntrepriseActiveId;
    }

    public static void SetDerniereEntrepriseActive(PaieDbContext db, int entrepriseId)
    {
        if (entrepriseId <= 0) return;
        var g = GetLigneGlobale(db);
        g.DerniereEntrepriseActiveId = entrepriseId;
        g.DateDerniereModification = DateTime.UtcNow;
        db.SaveChanges();
    }

    public static decimal GetTauxCdfParUsd(PaieDbContext db)
    {
        var p = GetParametresEntrepriseCourante(db);
        return p.TauxCdfParUsd > 0 ? p.TauxCdfParUsd : TauxParDefaut;
    }

    public static void SetTauxCdfParUsd(PaieDbContext db, decimal taux, bool mettreAJourPeriodesNonCloturees)
    {
        if (taux <= 0)
            throw new ArgumentOutOfRangeException(nameof(taux), "Le taux doit être supérieur à 0.");

        var p = GetParametresEntrepriseCourante(db);
        p.TauxCdfParUsd = decimal.Round(taux, 4, MidpointRounding.AwayFromZero);
        p.DateDerniereModification = DateTime.UtcNow;

        if (mettreAJourPeriodesNonCloturees)
        {
            foreach (var per in db.PeriodesPaie.Where(x => !x.Cloturee))
                per.TauxChangeBudget = p.TauxCdfParUsd;
        }

        db.SaveChanges();
    }

    public static bool GetForcerAssistantProchainDemarrage(PaieDbContext db) =>
        GetLigneGlobale(db).ForcerAssistantProchainDemarrage;

    public static void SetForcerAssistantProchainDemarrage(PaieDbContext db, bool valeur)
    {
        var p = GetLigneGlobale(db);
        p.ForcerAssistantProchainDemarrage = valeur;
        p.DateDerniereModification = DateTime.UtcNow;
        db.SaveChanges();
    }

    public static int GetVersionParcoursDemarrage(PaieDbContext db) =>
        GetLigneGlobale(db).VersionParcoursDemarrage;

    public static void SetVersionParcoursDemarrage(PaieDbContext db, int version)
    {
        var p = GetLigneGlobale(db);
        p.VersionParcoursDemarrage = version;
        p.DateDerniereModification = DateTime.UtcNow;
        db.SaveChanges();
    }

    private static ParametresApplication CreerParametresParDefaut(int id, int? entrepriseId) => new()
    {
        Id = id,
        EntrepriseId = entrepriseId,
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
        LtHeureFinSamedi = "12:30",
        LtModePointage = LtReglesPointageModes.QuatrePointages,
        LtDeductionPauseAutomatique = true
    };
}
