using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using MelodyPaieRDC.Helpers;

namespace MelodyPaieRDC;

/// <summary>
/// Point d'entrée explicite : fixe le répertoire courant sur le dossier de l'exe (installateur / raccourcis).
/// Ne pas utiliser SetDllDirectory ici : il retire le CWD de l'ordre de recherche Windows et provoque des
/// TypeInitializationException sur System.Windows.Application (ex. chargement de System.Memory manquant).
/// </summary>
internal static class Program
{
    [STAThread]
    public static void Main()
    {
        var exeDir = ResoudreRepertoireExe();
        try
        {
            if (!string.IsNullOrEmpty(exeDir))
                Directory.SetCurrentDirectory(exeDir);

            StartupLog.Append(
                $"Bootstrap Main : CWD={Environment.CurrentDirectory}, exeDir={exeDir}, ProcessPath={Environment.ProcessPath}, Assembly.Location={Assembly.GetExecutingAssembly().Location}, OS={Environment.OSVersion.VersionString}, x64={Environment.Is64BitOperatingSystem}");
        }
        catch
        {
            /* ne jamais empêcher le lancement */
        }

        try
        {
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
        catch (Exception ex)
        {
            try
            {
                StartupLog.Append("Échec avant ou pendant InitializeComponent / Run (ex. ressources App.xaml)", ex);
            }
            catch
            {
                /* ignore */
            }

            try
            {
                var log = StartupLog.CheminFichier();
                var detail = FormaterExceptionPourAffichage(ex);
                var suffix = string.IsNullOrEmpty(log) ? "" : $"\n\nJournal : {log}";
                MessageBox.Show(
                    $"Melody Paie RDC n'a pas pu démarrer.\n\n{detail}{suffix}",
                    "Melody Paie RDC",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch
            {
                try
                {
                    File.AppendAllText(
                        Path.Combine(Path.GetTempPath(), "MelodyPaieRDC_fatal.txt"),
                        $"{DateTime.UtcNow:O}\n{ex}\n");
                }
                catch
                {
                    /* dernier recours */
                }
            }
        }
    }

    private static string FormaterExceptionPourAffichage(Exception ex)
    {
        var sb = new StringBuilder();
        for (var e = ex; e != null; e = e.InnerException!)
        {
            if (sb.Length > 0) sb.AppendLine().AppendLine("---");
            sb.Append(e.GetType().Name).Append(": ").Append(e.Message);
        }
        return sb.ToString();
    }

    private static string? ResoudreRepertoireExe()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            var d = Path.GetDirectoryName(processPath);
            if (!string.IsNullOrEmpty(d)) return d;
        }

        var loc = Assembly.GetExecutingAssembly().Location;
        if (!string.IsNullOrEmpty(loc))
        {
            var d = Path.GetDirectoryName(loc);
            if (!string.IsNullOrEmpty(d)) return d;
        }

        try
        {
            var baseDir = AppContext.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir))
                return Path.GetFullPath(baseDir);
        }
        catch
        {
            /* ignore */
        }

        return null;
    }
}
