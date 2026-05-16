using System.Diagnostics;

namespace MelodyPaieRDC.Helpers;

/// <summary>Ouvre le navigateur par défaut (site Impact Entreprises, etc.).</summary>
public static class BrowseUriHelper
{
    public const string UrlImpactEntreprises = "https://impact-entreprises.net/";

    public static void OpenImpactEntreprises() => Open(UrlImpactEntreprises);

    public static void Open(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url.Trim()) { UseShellExecute = true });
        }
        catch
        {
            /* ignoré */
        }
    }
}
