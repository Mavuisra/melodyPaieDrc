using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

public sealed class LivrePaieSyncResultat
{
    public bool Ok { get; init; }
    public string Message { get; init; } = "";
    public int NbBulletins { get; init; }
    public DateTime? HorodatageUtc { get; init; }
}

public static class LivrePaieSyncService
{
    public static LivrePaieSyncResultat Synchroniser(PaieDbContext db, int periodePaieId)
    {
        var periode = db.PeriodesPaie.AsNoTracking().FirstOrDefault(p => p.Id == periodePaieId);
        if (periode == null)
            return new LivrePaieSyncResultat { Ok = false, Message = "Période de paie introuvable." };

        var bulletins = db.BulletinsPaie
            .AsNoTracking()
            .Where(b => b.PeriodePaieId == periodePaieId)
            .Select(b => new { b.NetAPayer, b.DateGeneration })
            .ToList();
        var nb = bulletins.Count;
        var netTotal = bulletins.Sum(b => b.NetAPayer);
        var now = DateTime.UtcNow;
        var p = db.ParametresApplication.FirstOrDefault(x => x.Id == ParametresApplication.SingletonId);
        if (p != null)
        {
            p.LivrePaieDerniereSyncUtc = now;
            db.SaveChanges();
        }

        if (nb == 0)
        {
            return new LivrePaieSyncResultat
            {
                Ok = false,
                Message = $"Aucun bulletin pour {periode.Mois:D2}/{periode.Annee}. Générez d’abord les bulletins.",
                NbBulletins = 0,
                HorodatageUtc = now
            };
        }

        return new LivrePaieSyncResultat
        {
            Ok = true,
            Message = $"Livre synchronisé — {nb} bulletin(s), net {netTotal:N2}, période {periode.Mois:D2}/{periode.Annee}.",
            NbBulletins = nb,
            HorodatageUtc = now
        };
    }
}
