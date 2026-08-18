using System.Text.RegularExpressions;

namespace MelodyPaieRDC.Tests;

/// <summary>
/// Empêche les liaisons TwoWay implicites (Popup.IsOpen, etc.) sur des propriétés en lecture seule,
/// qui font planter l'application au démarrage.
/// </summary>
public class XamlLiaisonLectureSeuleTests
{
    private static readonly Regex PopupIsOpenSansMode = new(
        @"<Popup\b[^>]*\bIsOpen\s*=\s*""\{Binding(?![^""]*Mode\s*=\s*OneWay)[^""]*\}""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    [Fact]
    public void Popup_IsOpen_est_toujours_en_OneWay()
    {
        var racine = TrouverRacineDepot();
        var fichiers = Directory.GetFiles(Path.Combine(racine, "Views"), "*.xaml", SearchOption.AllDirectories);
        Assert.NotEmpty(fichiers);

        var fautes = new List<string>();
        foreach (var fichier in fichiers)
        {
            var xaml = File.ReadAllText(fichier);
            if (PopupIsOpenSansMode.IsMatch(xaml))
                fautes.Add(Path.GetRelativePath(racine, fichier));
        }

        Assert.True(fautes.Count == 0,
            "Popup.IsOpen est TwoWay par défaut. Ajoutez Mode=OneWay si la propriété VM est en lecture seule. " +
            "Fichiers : " + string.Join(", ", fautes));
    }

    [Fact]
    public void BarreRecherchePointage_lie_AfficherSuggestions_en_OneWay()
    {
        var racine = TrouverRacineDepot();
        var xaml = File.ReadAllText(Path.Combine(racine, "Views", "SuiviJournalierPanel.xaml"));
        Assert.Contains("IsOpen=\"{Binding AfficherSuggestionsEmployes, Mode=OneWay}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("IsOpen=\"{Binding AfficherSuggestionsEmployes}\"", xaml, StringComparison.Ordinal);
    }

    private static string TrouverRacineDepot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MelodyPaieRDC.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Racine du dépôt MelodyPaieRDC introuvable.");
    }
}
