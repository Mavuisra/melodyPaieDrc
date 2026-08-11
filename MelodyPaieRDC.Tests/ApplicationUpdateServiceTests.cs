using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.Tests;

public class ApplicationUpdateServiceTests
{
    [Fact]
    public void EvaluerManifeste_VersionSuperieure_SignaleMiseAJour()
    {
        var manifest = new UpdateManifest
        {
            Version = "1.0.7",
            DownloadUrl = "https://example.com/setup.exe",
            FileName = "MelodyPaieRDC_Setup_1.0.7.exe"
        };
        var installee = new Version(1, 0, 6);

        var result = ApplicationUpdateService.EvaluerManifeste(manifest, installee);

        Assert.Equal(UpdateCheckResultKind.UpdateAvailable, result.Kind);
        Assert.True(result.VersionDisponible > installee);
        Assert.Contains("1.0.7", result.Message);
    }

    [Fact]
    public void EvaluerManifeste_VersionEgale_SignaleAJour()
    {
        var manifest = new UpdateManifest
        {
            Version = "1.0.6",
            DownloadUrl = "https://example.com/setup.exe"
        };
        var installee = new Version(1, 0, 6);

        var result = ApplicationUpdateService.EvaluerManifeste(manifest, installee);

        Assert.Equal(UpdateCheckResultKind.UpToDate, result.Kind);
    }

    [Fact]
    public void EvaluerManifeste_UrlManquante_RetourneErreur()
    {
        var manifest = new UpdateManifest { Version = "2.0.0", DownloadUrl = "" };
        var installee = new Version(1, 0, 0);

        var result = ApplicationUpdateService.EvaluerManifeste(manifest, installee);

        Assert.Equal(UpdateCheckResultKind.Error, result.Kind);
    }

    [Fact]
    public void ObtenirNomFichierInstallateur_UtiliseFileNameDuManifeste()
    {
        var manifest = new UpdateManifest
        {
            Version = "1.0.7",
            FileName = "MelodyPaieRDC_Setup_1.0.7.exe"
        };
        var uri = new Uri("https://github.com/org/repo/releases/download/v1.0.7/other.exe");

        var nom = ApplicationUpdateService.ObtenirNomFichierInstallateur(manifest, uri);

        Assert.Equal("MelodyPaieRDC_Setup_1.0.7.exe", nom);
    }

    [Fact]
    public void ObtenirNomFichierInstallateur_SansFileName_DeduitDepuisUrl()
    {
        var manifest = new UpdateManifest { Version = "1.0.7" };
        var uri = new Uri("https://github.com/org/repo/releases/download/v1.0.7/MelodyPaieRDC_Setup_1.0.7.exe");

        var nom = ApplicationUpdateService.ObtenirNomFichierInstallateur(manifest, uri);

        Assert.Equal("MelodyPaieRDC_Setup_1.0.7.exe", nom);
    }

    [Fact]
    public void FormaterVersion_SansRevisionSignificative_AfficheTroisSegments()
    {
        var v = new Version(1, 0, 7, 0);
        Assert.Equal("1.0.7", ApplicationUpdateService.FormaterVersion(v));
    }
}
