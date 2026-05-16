using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Synchronisation périodique du terminal ZKTeco vers le suivi journalier (timer UI).
/// Toutes les lectures passent par un verrou unique pour éviter plusieurs processus ZktecoPullWorker concurrents.
/// </summary>
public static class ZktecoSynchronisationService
{
    private static DispatcherTimer? _timer;
    private static readonly SemaphoreSlim SyncLock = new(1, 1);
    private static int _syncEnCours;
    public static event Action<DateTime>? SynchroReussie;
    public static event Action? SynchroEnCours;
    public static event Action<string>? SynchroErreur;

    /// <summary>Redémarre le timer selon les paramètres en base (au démarrage ou après enregistrement).</summary>
    public static void Reconfigurer()
    {
        _timer?.Stop();
        _timer = null;

        if (Application.Current?.Dispatcher == null)
            return;

        using var db = new PaieDbContext();
        ParametresApplicationHelper.EnsureRow(db);
        var p = db.ParametresApplication.AsNoTracking().FirstOrDefault(x => x.Id == ParametresApplication.SingletonId);
        if (p == null || !p.ZkSyncActif || string.IsNullOrWhiteSpace(p.ZkTerminalIp))
            return;

        var sec = Math.Clamp(p.ZkIntervalleSecondes <= 0 ? 60 : p.ZkIntervalleSecondes, 5, 3600);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(sec) };
        _timer.Tick += async (_, _) => { await TrySynchroniserSansChevauchementAsync(); };
        _timer.Start();

        _ = TrySynchroniserSansChevauchementAsync();
    }

    /// <summary>Une lecture terminal + fusion base. Sérialisé globalement.</summary>
    public static bool TrySynchroniser(out string? messageErreur)
    {
        SyncLock.Wait();
        try
        {
            return ExecuteSynchronisation(out _, out messageErreur);
        }
        finally
        {
            SyncLock.Release();
        }
    }

    /// <summary>Même synchro avec les journaux lus (évite une deuxième lecture pour l’affichage présence).</summary>
    public static bool TrySynchroniser(
        out IReadOnlyList<(string CodePin, DateTime Horodatage)>? logsLus,
        out string? messageErreur)
    {
        SyncLock.Wait();
        try
        {
            return ExecuteSynchronisation(out logsLus, out messageErreur);
        }
        finally
        {
            SyncLock.Release();
        }
    }

    /// <summary>Synchro asynchrone avec journaux ; même file d’attente que le timer global et que <see cref="TrySynchroniser"/>.</summary>
    public static async Task<(bool Ok, IReadOnlyList<(string CodePin, DateTime Horodatage)>? Logs, string? Err)> TrySynchroniserAvecLogsAsync(
        CancellationToken cancellationToken = default)
    {
        await SyncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SynchroEnCours?.Invoke();
            return await Task.Run(() =>
            {
                var ok = ExecuteSynchronisation(out var logs, out var err);
                return (ok, logs, err);
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            SyncLock.Release();
        }
    }

    private static bool ExecuteSynchronisation(
        out IReadOnlyList<(string CodePin, DateTime Horodatage)>? logs,
        out string? messageErreur)
    {
        logs = null;
        messageErreur = null;
        try
        {
            using var db = new PaieDbContext();
            ParametresApplicationHelper.EnsureRow(db);
            var p = db.ParametresApplication.FirstOrDefault(x => x.Id == ParametresApplication.SingletonId);
            if (p == null || string.IsNullOrWhiteSpace(p.ZkTerminalIp))
            {
                messageErreur = "Adresse IP du terminal non configurée.";
                return false;
            }

            var ip = p.ZkTerminalIp.Trim();
            var port = p.ZkTerminalPort > 0 ? p.ZkTerminalPort : 4370;
            var machine = p.ZkMachineNumber > 0 ? p.ZkMachineNumber : 1;

            try
            {
                logs = ZktecoPointageReader.Lire(ip, port, machine, p.ZkCommPassword);
            }
            catch (Exception ex)
            {
                messageErreur = ex.Message;
                return false;
            }

            var pointages = new PointageImportService(db);
            pointages.FusionnerPoinconsDepuisTerminal(logs);

            var maj = db.ParametresApplication.First(x => x.Id == ParametresApplication.SingletonId);
            maj.ZkDerniereSyncUtc = DateTime.UtcNow;
            db.SaveChanges();
            SynchroReussie?.Invoke(maj.ZkDerniereSyncUtc.Value);
            ZkTerminalParametresNotifier.Raise();
            return true;
        }
        catch (Exception ex)
        {
            messageErreur = ex.Message;
            return false;
        }
    }

    private static async Task TrySynchroniserSansChevauchementAsync()
    {
        if (Interlocked.Exchange(ref _syncEnCours, 1) == 1)
            return;

        try
        {
            var (ok, _, msg) = await TrySynchroniserAvecLogsAsync().ConfigureAwait(true);
            if (!ok)
                SynchroErreur?.Invoke(msg ?? "Erreur de synchronisation ZKTeco.");
        }
        finally
        {
            Interlocked.Exchange(ref _syncEnCours, 0);
        }
    }
}
