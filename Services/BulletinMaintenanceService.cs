using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Maintenance des bulletins (suppression et restauration de cohérence).
/// </summary>
public class BulletinMaintenanceService
{
    private readonly PaieDbContext _db;

    public BulletinMaintenanceService(PaieDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Supprime des bulletins et restaure, au mieux, les soldes de prêts/avances associés.
    /// </summary>
    /// <returns>Nombre de bulletins effectivement supprimés.</returns>
    public int SupprimerBulletins(IEnumerable<int> bulletinIds)
    {
        var ids = bulletinIds?.Distinct().ToList() ?? new List<int>();
        if (ids.Count == 0) return 0;

        var bulletins = _db.BulletinsPaie
            .Include(b => b.Details)
            .Include(b => b.PeriodePaie)
            .Where(b => ids.Contains(b.Id))
            .ToList();

        if (bulletins.Count == 0) return 0;

        foreach (var bulletin in bulletins)
            RestaurerSoldePrets(bulletin);

        _db.BulletinsPaie.RemoveRange(bulletins);
        _db.SaveChanges();
        return bulletins.Count;
    }

    private void RestaurerSoldePrets(BulletinPaie bulletin)
    {
        var retenuePrets = decimal.Round(
            bulletin.Details.FirstOrDefault(d => string.Equals(d.Libelle, "Prêts / avances", StringComparison.OrdinalIgnoreCase))?.Retenue ?? 0m,
            2);
        if (retenuePrets <= 0) return;

        var mois = bulletin.PeriodePaie?.Mois ?? DateTime.Today.Month;
        var annee = bulletin.PeriodePaie?.Annee ?? DateTime.Today.Year;
        var dateFinPeriode = new DateTime(annee, mois, 1).AddMonths(1).AddDays(-1);

        // Approximation contrôlée :
        // on crédite les prêts de l'employé potentiellement affectés par la période, sans dépasser le montant total.
        var prets = _db.PretsAvances
            .Where(p => p.EmployeId == bulletin.EmployeId &&
                        p.DateOctroi <= dateFinPeriode &&
                        p.SoldeRestant < p.MontantTotal)
            .OrderByDescending(p => p.DateOctroi)
            .ToList();

        var reste = retenuePrets;
        foreach (var p in prets)
        {
            if (reste <= 0) break;
            var capacite = p.MontantTotal - p.SoldeRestant;
            if (capacite <= 0) continue;

            var restauration = Math.Min(Math.Min(p.MontantMensuel, reste), capacite);
            p.SoldeRestant = decimal.Round(p.SoldeRestant + restauration, 2);
            if (p.SoldeRestant > 0 && string.Equals(p.Statut, "Terminé", StringComparison.OrdinalIgnoreCase))
                p.Statut = "En cours";
            reste -= restauration;
        }
    }
}
