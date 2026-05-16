using System.Text.Json;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Enregistre les changements locaux pour une synchronisation cloud ultérieure (offline-first).
/// </summary>
public class SyncJournalService
{
    private readonly PaieDbContext _db;

    public SyncJournalService(PaieDbContext db) => _db = db;

    public void EnregistrerModification(string nomTable, int enregistrementId, string operation, object? payload = null, int? entrepriseId = null)
    {
        var syncParams = entrepriseId.HasValue
            ? _db.SyncParametres.FirstOrDefault(s => s.EntrepriseId == entrepriseId && s.SyncActive)
            : null;

        if (syncParams == null && entrepriseId == null)
            return;

        _db.SyncJournaux.Add(new SyncJournal
        {
            EntrepriseId = entrepriseId,
            NomTable = nomTable,
            EnregistrementId = enregistrementId,
            Operation = operation,
            PayloadJson = payload != null ? JsonSerializer.Serialize(payload) : null,
            DateModificationUtc = DateTime.UtcNow,
            DeviceId = syncParams?.DeviceId
        });
        _db.SaveChanges();
    }

    public IReadOnlyList<SyncJournal> ObtenirEnAttente(int? entrepriseId = null, int max = 500)
    {
        var q = _db.SyncJournaux.Where(j => j.DateSyncUtc == null);
        if (entrepriseId.HasValue)
            q = q.Where(j => j.EntrepriseId == null || j.EntrepriseId == entrepriseId);
        return q.OrderBy(j => j.DateModificationUtc).Take(max).ToList();
    }

    public void MarquerSynchronise(long journalId)
    {
        var entree = _db.SyncJournaux.Find(journalId);
        if (entree == null) return;
        entree.DateSyncUtc = DateTime.UtcNow;
        _db.SaveChanges();
    }
}
