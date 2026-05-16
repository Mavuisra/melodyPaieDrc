using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>Lecture des paramètres réseau ZKTeco depuis la base (source unique avec l’onglet Paramètres).</summary>
public static class ZkTerminalParametresResolver
{
    /// <summary>Recharge la ligne singleton depuis la base pour éviter un cache EF obsolète après sauvegarde depuis un autre <see cref="PaieDbContext"/>.</summary>
    public static ParametresApplication? ObtenirParametresZkFresh(PaieDbContext db)
    {
        ParametresApplicationHelper.EnsureRow(db);
        var p = db.ParametresApplication.Find(ParametresApplication.SingletonId);
        if (p != null)
            db.Entry(p).Reload();
        return p;
    }

    public static string FormaterResumeConnexion(ParametresApplication p)
    {
        var ip = string.IsNullOrWhiteSpace(p.ZkTerminalIp)
            ? "(IP non renseignée — voir Paramètres > ZKTeco)"
            : p.ZkTerminalIp.Trim();
        var port = p.ZkTerminalPort > 0 ? p.ZkTerminalPort : 4370;
        var machine = p.ZkMachineNumber > 0 ? p.ZkMachineNumber : 1;
        var intervalle = p.ZkIntervalleSecondes > 0 ? p.ZkIntervalleSecondes : 60;
        var sync = p.ZkSyncActif ? "activée" : "désactivée";
        return $"{ip}:{port} · machine {machine} · intervalle {intervalle}s · synchro auto {sync}";
    }

    /// <summary>Paramètres réseau nécessaires pour lire le terminal (pointage en direct, import utilisateurs).</summary>
    public static bool TryGetConnexion(
        PaieDbContext db,
        out string ip,
        out int port,
        out int machine,
        out int intervalleSecondes,
        out int commPassword,
        out string? erreur)
    {
        ip = "";
        port = 4370;
        machine = 1;
        intervalleSecondes = 60;
        commPassword = 0;
        erreur = null;

        var p = ObtenirParametresZkFresh(db);
        if (p == null)
        {
            erreur = "Paramètres application introuvables.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(p.ZkTerminalIp))
        {
            erreur = "Indiquez l’adresse IP du terminal dans Paramètres > ZKTeco.";
            return false;
        }

        ip = p.ZkTerminalIp.Trim();
        port = p.ZkTerminalPort > 0 ? p.ZkTerminalPort : 4370;
        machine = p.ZkMachineNumber > 0 ? p.ZkMachineNumber : 1;
        intervalleSecondes = p.ZkIntervalleSecondes > 0 ? p.ZkIntervalleSecondes : 60;
        commPassword = p.ZkCommPassword;

        if (port <= 0 || port > 65535)
        {
            erreur = "Port ZKTeco invalide en base (1–65535). Corrigez dans Paramètres > ZKTeco.";
            return false;
        }

        if (machine <= 0)
        {
            erreur = "Numéro de machine invalide en base. Corrigez dans Paramètres > ZKTeco.";
            return false;
        }

        if (intervalleSecondes < 5 || intervalleSecondes > 3600)
        {
            erreur = "Intervalle de synchro invalide en base (5–3600 s). Corrigez dans Paramètres > ZKTeco.";
            return false;
        }

        if (commPassword < 0)
        {
            erreur = "Clé comm invalide en base. Corrigez dans Paramètres > ZKTeco.";
            return false;
        }

        return true;
    }
}
