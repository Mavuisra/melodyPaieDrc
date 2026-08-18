using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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
    private const int DelaiTelechargementMinutes = 20;
    private const int TentativesFichier = 8;

    private static readonly HttpClient Http = CreerHttpClient();
    private static readonly HttpClient HttpTelechargement = CreerHttpClientTelechargement();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static HttpClient CreerHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "MelodyPaieRDC-Updater/1.0");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        return client;
    }

    private static HttpClient CreerHttpClientTelechargement()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(DelaiTelechargementMinutes) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "MelodyPaieRDC-Updater/1.0");
        return client;
    }

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

        var urlsManifeste = new List<string>();
        if (!string.IsNullOrWhiteSpace(config.ManifestUrl))
            urlsManifeste.Add(config.ManifestUrl.Trim());
        if (!urlsManifeste.Contains(ApplicationUpdateDefaults.ManifestUrlParDefaut, StringComparer.OrdinalIgnoreCase))
            urlsManifeste.Add(ApplicationUpdateDefaults.ManifestUrlParDefaut);

        foreach (var url in urlsManifeste)
        {
            try
            {
                var result = await VerifierDepuisManifesteAsync(url, installee, cancellationToken).ConfigureAwait(false);
                if (result.Kind != UpdateCheckResultKind.Error ||
                    !result.Message.Contains("404", StringComparison.Ordinal))
                    return result;
            }
            catch
            {
                // On tente l'URL suivante, puis GitHub Releases.
            }
        }

        try
        {
            return await VerifierDepuisGitHubReleasesAsync(installee, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Message utilisateur ci-dessous, sans détail technique anglais.
        }

        return new UpdateCheckResult
        {
            Kind = UpdateCheckResultKind.Error,
            Message =
                "Impossible de joindre le serveur de mises à jour. " +
                "Vérifiez votre connexion Internet, puis réessayez.",
            VersionInstallee = installee
        };
    }

    private static async Task<UpdateCheckResult> VerifierDepuisManifesteAsync(
        string url,
        Version installee,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return new UpdateCheckResult
            {
                Kind = UpdateCheckResultKind.Error,
                Message = "URL du manifeste invalide (http ou https requis).",
                VersionInstallee = installee
            };
        }

        var json = await Http.GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
        var manifest = JsonSerializer.Deserialize<UpdateManifest>(json, JsonOptions);
        return EvaluerManifeste(manifest, installee);
    }

    private static async Task<UpdateCheckResult> VerifierDepuisGitHubReleasesAsync(
        Version installee,
        CancellationToken cancellationToken)
    {
        var json = await Http.GetStringAsync(ApplicationUpdateDefaults.ReleasesLatestApiUrl, cancellationToken)
            .ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tag = root.GetProperty("tag_name").GetString() ?? "";
        var versionText = tag.TrimStart('v', 'V');
        if (!Version.TryParse(versionText, out var disponible))
            throw new InvalidOperationException($"Tag de release invalide : {tag}");

        string? downloadUrl = null;
        string? fileName = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    continue;
                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                fileName = name;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
            throw new InvalidOperationException("Aucun installateur .exe dans la dernière release GitHub.");

        var notes = root.TryGetProperty("body", out var body) ? body.GetString() : null;
        var manifest = new UpdateManifest
        {
            Version = versionText,
            DownloadUrl = downloadUrl,
            FileName = fileName,
            ReleaseNotes = string.IsNullOrWhiteSpace(notes) ? $"Release {tag}" : notes
        };

        return EvaluerManifeste(manifest, installee);
    }

    internal static UpdateCheckResult EvaluerManifeste(UpdateManifest? manifest, Version installee)
    {
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
                Message = $"Numéro de version invalide : « {manifest.Version} ».",
                VersionInstallee = installee
            };
        }

        if (string.IsNullOrWhiteSpace(manifest.DownloadUrl))
        {
            return new UpdateCheckResult
            {
                Kind = UpdateCheckResultKind.Error,
                Message = "L'adresse de téléchargement est absente du serveur de mises à jour.",
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
        var cheminFinal = Path.Combine(DossierTelechargements, nomFichier);
        var cheminPartiel = cheminFinal + ".part";
        SupprimerFichierSilencieux(cheminPartiel);

        try
        {
            using var response = await HttpTelechargement
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength;
            string empreinte;
            await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var fichier = new FileStream(
                             cheminPartiel,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.SequentialScan | FileOptions.Asynchronous))
            using (var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[81920];
                long lu = 0;
                int lus;
                while ((lus = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await fichier.WriteAsync(buffer.AsMemory(0, lus), cancellationToken).ConfigureAwait(false);
                    hasher.AppendData(buffer.AsSpan(0, lus));
                    lu += lus;
                    if (total is > 0)
                        progression?.Report(Math.Min(99.0, lu * 99.0 / total.Value));
                }

                await fichier.FlushAsync(cancellationToken).ConfigureAwait(false);
                empreinte = Convert.ToHexString(hasher.GetHashAndReset());
            }

            if (!string.IsNullOrWhiteSpace(manifest.Sha256))
            {
                var attendu = manifest.Sha256.Trim().Replace(" ", "", StringComparison.Ordinal);
                if (!string.Equals(empreinte, attendu, StringComparison.OrdinalIgnoreCase))
                {
                    SupprimerFichierSilencieux(cheminPartiel);
                    return new UpdateDownloadResult
                    {
                        Success = false,
                        Message = "Le fichier téléchargé est incomplet ou altéré. Réessayez le téléchargement."
                    };
                }
            }

            var cheminPret = await FinaliserFichierTelechargeAsync(cheminPartiel, cheminFinal, cancellationToken)
                .ConfigureAwait(false);
            progression?.Report(100.0);

            return new UpdateDownloadResult
            {
                Success = true,
                Message = "Téléchargement terminé. L'installation peut commencer.",
                CheminInstallateur = cheminPret
            };
        }
        catch (OperationCanceledException)
        {
            SupprimerFichierSilencieux(cheminPartiel);
            return new UpdateDownloadResult
            {
                Success = false,
                Message = "Le téléchargement a été annulé ou a pris trop de temps. Vérifiez la connexion, puis réessayez."
            };
        }
        catch (Exception ex)
        {
            SupprimerFichierSilencieux(cheminPartiel);
            return new UpdateDownloadResult
            {
                Success = false,
                Message = MessageUtilisateurTelechargement(ex)
            };
        }
    }

    internal static string MessageUtilisateurTelechargement(Exception ex)
    {
        if (EstFichierVerrouille(ex))
            return "Le fichier d'installation est encore utilisé par Windows ou un antivirus. " +
                   "Fermez les autres fenêtres Melody Paie RDC, attendez quelques secondes, puis réessayez.";

        if (ex is HttpRequestException)
            return "Le téléchargement a été interrompu. Vérifiez votre connexion Internet, puis réessayez.";

        if (ex is IOException)
            return "Impossible d'enregistrer le fichier d'installation sur cet ordinateur. Vérifiez l'espace disque, puis réessayez.";

        return "La mise à jour n'a pas pu aboutir. Réessayez dans un instant.";
    }

    internal static bool EstFichierVerrouille(Exception ex)
    {
        for (var courant = ex; courant != null; courant = courant.InnerException)
        {
            if (courant is IOException io && (io.HResult & 0xFFFF) is 32 or 33)
                return true;
        }
        return false;
    }

    private static async Task<string> FinaliserFichierTelechargeAsync(
        string cheminPartiel,
        string cheminFinal,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < TentativesFichier; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (File.Exists(cheminFinal))
                    File.Delete(cheminFinal);
                File.Move(cheminPartiel, cheminFinal);
                return cheminFinal;
            }
            catch (IOException) when (i < TentativesFichier - 1)
            {
                await Task.Delay(350, cancellationToken).ConfigureAwait(false);
            }
        }

        var unique = Path.Combine(
            Path.GetDirectoryName(cheminFinal) ?? DossierTelechargements,
            $"{Path.GetFileNameWithoutExtension(cheminFinal)}_{DateTime.Now:HHmmss}.exe");
        File.Move(cheminPartiel, unique);
        return unique;
    }

    private static void SupprimerFichierSilencieux(string chemin)
    {
        try
        {
            if (File.Exists(chemin))
                File.Delete(chemin);
        }
        catch
        {
            // Antivirus ou explorateur : on n'empêche pas la suite.
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
            message = "L'installateur s'est ouvert. Fermez Melody Paie RDC pour terminer l'installation.";
            return true;
        }
        catch (Exception ex)
        {
            message = EstFichierVerrouille(ex)
                ? "L'installateur est encore utilisé par Windows ou un antivirus. Attendez quelques secondes, puis réessayez."
                : "Windows n'a pas pu ouvrir l'installateur. Réessayez, ou lancez le fichier depuis Paramètres > Mises à jour.";
            return false;
        }
    }

    public static string ObtenirCheminExecutableCourant()
    {
        try
        {
            var chemin = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(chemin) && File.Exists(chemin))
                return chemin;
        }
        catch
        {
            // ignore
        }

        return Path.Combine(AppContext.BaseDirectory, "MelodyPaieRDC.exe");
    }

    /// <summary>
    /// Lance l'installateur en mode silencieux puis relance l'application (script PowerShell détaché).
    /// </summary>
    public static bool LancerMiseAJourSilencieuseEtRelancer(string cheminInstallateur, out string message)
    {
        message = "";
        if (string.IsNullOrWhiteSpace(cheminInstallateur) || !File.Exists(cheminInstallateur))
        {
            message = "Fichier d'installation introuvable.";
            return false;
        }

        try
        {
            var exePath = ObtenirCheminExecutableCourant();
            Directory.CreateDirectory(DossierTelechargements);
            var scriptPath = Path.Combine(DossierTelechargements, "apply-update.ps1");

            var script = new StringBuilder();
            script.AppendLine("$ErrorActionPreference = 'SilentlyContinue'");
            script.AppendLine($"$installer = '{EchapperPourPowerShell(cheminInstallateur)}'");
            script.AppendLine($"$exe = '{EchapperPourPowerShell(exePath)}'");
            script.AppendLine("Start-Sleep -Seconds 2");
            script.AppendLine("$p = $null");
            script.AppendLine("for ($i = 0; $i -lt 8 -and $null -eq $p; $i++) {");
            script.AppendLine("  $p = Start-Process -FilePath $installer -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/CLOSEAPPLICATIONS','/NORESTART' -PassThru -Wait");
            script.AppendLine("  if ($null -eq $p) { Start-Sleep -Milliseconds 400 }");
            script.AppendLine("}");
            script.AppendLine("if ($null -ne $p -and ($p.ExitCode -eq 0 -or $p.ExitCode -eq 3010)) {");
            script.AppendLine("  Start-Sleep -Seconds 2");
            script.AppendLine("  if (Test-Path -LiteralPath $exe) { Start-Process -FilePath $exe }");
            script.AppendLine("}");

            File.WriteAllText(scriptPath, script.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            message = "L'installation va se terminer, puis Melody Paie RDC redémarrera tout seul.";
            return true;
        }
        catch (Exception ex)
        {
            message = EstFichierVerrouille(ex)
                ? "Le fichier d'installation est encore utilisé par Windows ou un antivirus. Fermez les autres fenêtres Melody Paie RDC, puis réessayez."
                : "L'installation n'a pas pu démarrer. Réessayez depuis Paramètres > Mises à jour.";
            return false;
        }
    }

    internal static string ObtenirNomFichierInstallateur(UpdateManifest manifest, Uri uri)
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

    private static string EchapperPourPowerShell(string valeur) =>
        valeur.Replace("'", "''", StringComparison.Ordinal);
}
