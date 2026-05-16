using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

public enum UpdateCheckResultKind
{
    UpToDate,
    UpdateAvailable,
    Error,
    SkippedNoUrl
}

public sealed class UpdateCheckResult
{
    public UpdateCheckResultKind Kind { get; init; }
    public string Message { get; init; } = "";
    public UpdateManifest? Manifest { get; init; }
    public Version? VersionInstallee { get; init; }
    public Version? VersionDisponible { get; init; }
}

public sealed class UpdateDownloadResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public string? CheminInstallateur { get; init; }
}

/// <summary>
/// Vérification de version, téléchargement et lancement de l'installateur Inno Setup.
/// </summary>
public static class ApplicationUpdateService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(45)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string DossierTelechargements =>
        Path.Combine(PaieDbContext.GetDataDirectory(), "Updates");

    public static Version ObtenirVersionInstallee()
    {
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            var partie = info.Split('+')[0].Trim();
            if (Version.TryParse(partie, out var vInfo))
                return vInfo;
        }

        var v = asm.GetName().Version;
        if (v != null)
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));
        return new Version(1, 0, 0);
    }

    public static string FormaterVersion(Version version) =>
        version.Revision > 0 ? version.ToString(4) : version.ToString(3);

    public static async Task<UpdateCheckResult> VerifierAsync(CancellationToken cancellationToken = default)
    {
        var installee = ObtenirVersionInstallee();
        var config = UpdateConfigHelper.Charger();
        var url = config.ManifestUrl?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(url))
        {
            return new UpdateCheckResult
            {
                Kind = UpdateCheckResultKind.SkippedNoUrl,
                Message = "Aucune URL de mises à jour configurée.",
                VersionInstallee = installee
            };
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return new UpdateCheckResult
            {
                Kind = UpdateCheckResultKind.Error,
                Message = "L'URL du manifeste de mise à jour est invalide (http ou https requis).",
                VersionInstallee = installee
            };
        }

        try
        {
            var json = await Http.GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
            var manifest = JsonSerializer.Deserialize<UpdateManifest>(json, JsonOptions);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version))
            {
                return new UpdateCheckResult
                {
                    Kind = UpdateCheckResultKind.Error,
                    Message = "Le fichier de version est vide ou illisible.",
                    VersionInstallee = installee
                };
            }

            if (!Version.TryParse(manifest.Version.Trim(), out var disponible))
            {
                return new UpdateCheckResult
                {
                    Kind = UpdateCheckResultKind.Error,
                    Message = $"Numéro de version invalide dans le manifeste : « {manifest.Version} ».",
                    VersionInstallee = installee
                };
            }

            if (string.IsNullOrWhiteSpace(manifest.DownloadUrl))
            {
                return new UpdateCheckResult
                {
                    Kind = UpdateCheckResultKind.Error,
                    Message = "Le manifeste ne contient pas d'URL de téléchargement (downloadUrl).",
                    VersionInstallee = installee,
                    VersionDisponible = disponible,
                    Manifest = manifest
                };
            }

            if (disponible <= installee)
            {
                return new UpdateCheckResult
                {
                    Kind = UpdateCheckResultKind.UpToDate,
                    Message = $"Vous utilisez la dernière version ({FormaterVersion(installee)}).",
                    VersionInstallee = installee,
                    VersionDisponible = disponible,
                    Manifest = manifest
                };
            }

            return new UpdateCheckResult
            {
                Kind = UpdateCheckResultKind.UpdateAvailable,
                Message = $"La version {FormaterVersion(disponible)} est disponible (installée : {FormaterVersion(installee)}).",
                VersionInstallee = installee,
                VersionDisponible = disponible,
                Manifest = manifest
            };
        }
        catch (TaskCanceledException)
        {
            return new UpdateCheckResult
            {
                Kind = UpdateCheckResultKind.Error,
                Message = "Délai dépassé lors de la vérification des mises à jour.",
                VersionInstallee = installee
            };
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult
            {
                Kind = UpdateCheckResultKind.Error,
                Message = $"Impossible de vérifier les mises à jour : {ex.Message}",
                VersionInstallee = installee
            };
        }
    }

    public static async Task<UpdateDownloadResult> TelechargerAsync(
        UpdateManifest manifest,
        IProgress<double>? progression = null,
        CancellationToken cancellationToken = default)
    {
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.DownloadUrl))
            return new UpdateDownloadResult { Success = false, Message = "Manifeste ou URL de téléchargement manquant." };

        if (!Uri.TryCreate(manifest.DownloadUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return new UpdateDownloadResult { Success = false, Message = "URL de téléchargement invalide." };
        }

        var nomFichier = ObtenirNomFichierInstallateur(manifest, uri);
        if (!nomFichier.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return new UpdateDownloadResult
            {
                Success = false,
                Message = "Seuls les installateurs .exe sont acceptés."
            };
        }

        Directory.CreateDirectory(DossierTelechargements);
        var chemin = Path.Combine(DossierTelechargements, nomFichier);

        try
        {
            using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var fichier = new FileStream(chemin, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long lu = 0;
            int lus;
            while ((lus = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                await fichier.WriteAsync(buffer.AsMemory(0, lus), cancellationToken).ConfigureAwait(false);
                lu += lus;
                if (total is > 0)
                    progression?.Report(Math.Min(100.0, lu * 100.0 / total.Value));
            }

            progression?.Report(100.0);

            if (!string.IsNullOrWhiteSpace(manifest.Sha256))
            {
                var attendu = manifest.Sha256.Trim().Replace(" ", "", StringComparison.Ordinal);
                var bytes = await File.ReadAllBytesAsync(chemin, cancellationToken).ConfigureAwait(false);
                var calcule = Convert.ToHexString(SHA256.HashData(bytes));
                if (!string.Equals(calcule, attendu, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(chemin); } catch { /* ignore */ }
                    return new UpdateDownloadResult
                    {
                        Success = false,
                        Message = "L'empreinte SHA-256 du fichier téléchargé ne correspond pas au manifeste."
                    };
                }
            }

            return new UpdateDownloadResult
            {
                Success = true,
                Message = "Téléchargement terminé.",
                CheminInstallateur = chemin
            };
        }
        catch (TaskCanceledException)
        {
            return new UpdateDownloadResult { Success = false, Message = "Téléchargement annulé ou expiré." };
        }
        catch (Exception ex)
        {
            try { if (File.Exists(chemin)) File.Delete(chemin); } catch { /* ignore */ }
            return new UpdateDownloadResult { Success = false, Message = $"Échec du téléchargement : {ex.Message}" };
        }
    }

    public static bool LancerInstallateur(string cheminInstallateur, out string message)
    {
        message = "";
        if (string.IsNullOrWhiteSpace(cheminInstallateur) || !File.Exists(cheminInstallateur))
        {
            message = "Fichier d'installation introuvable.";
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = cheminInstallateur,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(cheminInstallateur) ?? DossierTelechargements
            });
            message = "L'installateur a été lancé. L'application va se fermer.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Impossible de lancer l'installateur : {ex.Message}";
            return false;
        }
    }

    private static string ObtenirNomFichierInstallateur(UpdateManifest manifest, Uri uri)
    {
        if (!string.IsNullOrWhiteSpace(manifest.FileName))
        {
            var nom = Path.GetFileName(manifest.FileName.Trim());
            if (!string.IsNullOrEmpty(nom))
                return nom;
        }

        var depuisUrl = Path.GetFileName(uri.LocalPath);
        if (!string.IsNullOrEmpty(depuisUrl) && depuisUrl.Contains('.'))
            return depuisUrl;

        var version = manifest.Version.Replace('.', '_');
        return $"MelodyPaieRDC_Setup_{version}.exe";
    }
}
