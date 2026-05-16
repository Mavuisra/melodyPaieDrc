using System.IO;

namespace MelodyPaieRDC.Services;

/// <summary>Guides d'utilisation des exports officiels (fichiers Markdown déployés avec l'application).</summary>
public static class GuidesExportService
{
    public const string GuideCnssRelative = "Guides/guide-edeclaration-cnss.md";
    public const string GuideIprRelative = "Guides/guide-declaration-ipr-dgi.md";

    public static string ObtenirCheminGuideCnss() => ResoudreChemin(GuideCnssRelative);
    public static string ObtenirCheminGuideIpr() => ResoudreChemin(GuideIprRelative);

    public static string LireGuideCnss() => LireSiExiste(GuideCnssRelative);
    public static string LireGuideIpr() => LireSiExiste(GuideIprRelative);

    private static string ResoudreChemin(string relative)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(baseDir, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string LireSiExiste(string relative)
    {
        var path = ResoudreChemin(relative);
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }
}
