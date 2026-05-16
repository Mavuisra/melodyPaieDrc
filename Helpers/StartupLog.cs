using System.IO;
using MelodyPaieRDC.Data;

namespace MelodyPaieRDC.Helpers;

/// <summary>
/// Journal minimal pour le support : reste lisible même si l'UI ne s'affiche pas.
/// Fichier : %LocalAppData%\MelodyPaieRDC\Logs\startup.log
/// </summary>
public static class StartupLog
{
    private static readonly object Verrou = new();

    public static void Append(string message, Exception? ex = null)
    {
        try
        {
            var logDir = Path.Combine(PaieDbContext.GetDataDirectory(), "..", "Logs");
            logDir = Path.GetFullPath(logDir);
            Directory.CreateDirectory(logDir);
            var path = Path.Combine(logDir, "startup.log");
            var ligne = $"{DateTime.UtcNow:O} | {message}";
            if (ex != null)
                ligne += $"{Environment.NewLine}{ex}";
            lock (Verrou)
                File.AppendAllText(path, ligne + Environment.NewLine + Environment.NewLine);
        }
        catch
        {
            /* ne jamais faire échouer le démarrage à cause du log */
        }
    }

    public static string? CheminFichier()
    {
        try
        {
            var logDir = Path.Combine(PaieDbContext.GetDataDirectory(), "..", "Logs");
            return Path.Combine(Path.GetFullPath(logDir), "startup.log");
        }
        catch
        {
            return null;
        }
    }
}
