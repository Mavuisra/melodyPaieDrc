using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Lecture des pointages via le programme auxiliaire <see cref="ZktecoPullWorker"/> (.NET Framework),
/// car le SDK ZKTeco provoque « Thread abort is not supported on this platform » sous .NET 8.
/// </summary>
public static class ZktecoPointageReader
{
    private const string DossierWorkerRelatif = "ZktecoPullWorker";

    private const int TimeoutMs = 120_000;
    /// <summary>Délai pour le test TCP avant lancement du worker (réseaux chargés ou terminal lent).</summary>
    private const int TcpProbeMs = 12_000;

    /// <summary>
    /// Lance ZktecoPullWorker.exe (sous-dossier <c>ZktecoPullWorker</c> ou ancien emplacement racine), renvoie les horodatages lus sur le terminal.
    /// </summary>
    public static IReadOnlyList<(string CodePin, DateTime Horodatage)> Lire(string ip, int port, int machineId,
        int commPassword = 0)
    {
        var exePath = ResoudreCheminExeWorker();
        if (exePath == null)
            throw new FileNotFoundException(
                "ZktecoPullWorker.exe est introuvable (dossier « ZktecoPullWorker » sous l’application ou ancien emplacement à la racine). Réinstallez ou recompilez la solution complète.");

        VerifierPortTcp(ip.Trim(), port);

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.ArgumentList.Add(ip.Trim());
        psi.ArgumentList.Add(port.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add(machineId.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add(commPassword.ToString(CultureInfo.InvariantCulture));

        using var process = new Process { StartInfo = psi };
        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();

        if (!process.WaitForExit(TimeoutMs))
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
                /* ignore */
            }

            throw new TimeoutException("La lecture du terminal ZKTeco a dépassé la durée maximale.");
        }

        if (process.ExitCode == 2)
            throw new InvalidOperationException("Programme auxiliaire ZKTeco : arguments invalides.");

        if (process.ExitCode != 0)
        {
            var msg = string.IsNullOrWhiteSpace(stderr) ? "Échec de la lecture ZKTeco." : stderr.Trim();
            if (msg.StartsWith("CONN_FAIL", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(ConstruireMessageEchecConnexion(ip.Trim(), port, msg));
            throw new InvalidOperationException(msg);
        }

        stdout = stdout.Trim();
        if (string.IsNullOrEmpty(stdout) || stdout == "[]")
            return Array.Empty<(string CodePin, DateTime Horodatage)>();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var rows = JsonSerializer.Deserialize<List<ZkPullRowDto>>(stdout, options);
        if (rows == null || rows.Count == 0)
            return Array.Empty<(string CodePin, DateTime Horodatage)>();

        var list = new List<(string CodePin, DateTime Horodatage)>();
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.p) || string.IsNullOrWhiteSpace(r.t))
                continue;
            if (!DateTime.TryParse(r.t, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                continue;
            dt = NormaliserHorodatageTerminal(dt);
            list.Add((r.p.Trim(), dt));
        }

        return list;
    }

    /// <summary>
    /// Le worker net48 et ses DLL doivent rester dans un sous-dossier pour ne pas écraser System.Memory etc. du runtime .NET 8.
    /// Rétrocompat : ancienne installation avec l’exe à la racine.
    /// </summary>
    private static string? ResoudreCheminExeWorker()
    {
        var nested = Path.Combine(AppContext.BaseDirectory, DossierWorkerRelatif, "ZktecoPullWorker.exe");
        if (File.Exists(nested))
            return nested;
        var legacy = Path.Combine(AppContext.BaseDirectory, "ZktecoPullWorker.exe");
        return File.Exists(legacy) ? legacy : null;
    }

    /// <summary>
    /// Normalise un horodatage venant du terminal : on travaille en heure locale métier.
    /// - UTC -> converti en local
    /// - Unspecified -> considéré local (pas de décalage appliqué)
    /// </summary>
    private static DateTime NormaliserHorodatageTerminal(DateTime dt)
    {
        if (dt.Kind == DateTimeKind.Utc)
            return dt.ToLocalTime();
        if (dt.Kind == DateTimeKind.Unspecified)
            return DateTime.SpecifyKind(dt, DateTimeKind.Local);
        return dt;
    }

    /// <summary>
    /// Vérifie qu’un socket TCP peut s’ouvrir avant le SDK : message plus clair que Connect_Net=false seul.
    /// </summary>
    private static void VerifierPortTcp(string ip, int port)
    {
        try
        {
            using var cts = new CancellationTokenSource(TcpProbeMs);
            using var client = new TcpClient();
            client.ConnectAsync(ip, port, cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            throw new InvalidOperationException(MessageEchecTcpDelai(ip, port));
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException(
                $"Impossible d’ouvrir une connexion TCP vers {ip}:{port} ({ex.SocketErrorCode}). Vérifiez l’IP, le port, le routage et le pare-feu.{AvertissementPortZkSiAtypique(port)}{RappelSousReseauEtPasserelle(ip)}{SuffixeDiagnosticTcp(ip, port)}",
                ex);
        }
    }

    private static string MessageEchecTcpDelai(string ip, int port) =>
        $"Aucune réponse sur {ip}:{port} dans les {TcpProbeMs / 1000} s." +
        " Vérifiez l’adresse IP (menu Communication / Réseau sur le terminal), que l’appareil est sous tension et sur le même réseau que ce PC (pas d’isolement Wi‑Fi « invité »), le port affiché sur le terminal (souvent 4370 pour ZKTeco), que la communication PC / push-pull n’est pas désactivée dans les options du boîtier, et le pare-feu Windows (connexion TCP sortante)." +
        AvertissementPortZkSiAtypique(port) +
        RappelSousReseauEtPasserelle(ip) +
        SuffixeDiagnosticTcp(ip, port);

    private static string SuffixeDiagnosticTcp(string ip, int port) =>
        $"{Environment.NewLine}{Environment.NewLine}Diagnostic sur ce PC (PowerShell) :{Environment.NewLine}" +
        $"Test-NetConnection -ComputerName {ip} -Port {port}{Environment.NewLine}" +
        "Si PingSucceeded = True mais TcpTestSucceeded = False, le port est fermé, filtré ou différent sur l’appareil. Si les deux sont False, l’IP est incorrecte ou le terminal injoignable.";

    /// <summary>Rappel réseau : même sous-réseau PC/terminal ; passerelle cohérente sur le boîtier.</summary>
    private static string RappelSousReseauEtPasserelle(string ip)
    {
        // Ex. 192.168.1.201 → préfixe 192.168.1 — message lisible sans parser masque variable.
        var parts = ip.Split('.');
        var prefix = parts.Length >= 3 ? $"{parts[0]}.{parts[1]}.{parts[2]}" : ip;
        return $"{Environment.NewLine}{Environment.NewLine}Sur ce PC, exécutez « ipconfig » : l’IPv4 doit être sur le **même sous-réseau** que le terminal (ex. PC **{prefix}.x** pour joindre **{ip}** avec masque 255.255.255.0). " +
               "Sur le terminal, le **portail (passerelle)** doit être une adresse de ce même sous-réseau (ex. la box en 192.168.1.1) ; un portail 192.168.0.x avec une IP 192.168.1.x est incohérent et peut empêcher tout accès depuis un PC mal routé.";
    }

    /// <summary>Ports souvent documentés pour la communication « machine » ZKTeco ; tout autre port déclenche un rappel.</summary>
    private static bool EstPortZktecoCourant(int port) =>
        port is 4370 or 4371 or 80 or 8080 or 5005 or 5200;

    private static string AvertissementPortZkSiAtypique(int port)
    {
        if (EstPortZktecoCourant(port))
            return "";

        return $"{Environment.NewLine}{Environment.NewLine}Le port {port} n’est pas un port courant pour la liaison réseau ZKTeco utilisée par Melody (le plus fréquent est 4370). " +
               "Ouvrez sur le terminal le menu Communication / Réseau et recopiez exactement le port TCP indiqué ; une faute de frappe (ex. 168 au lieu de 4370) bloque la connexion.";
    }

    private static string ConstruireMessageEchecConnexion(string ip, int port, string stderrLigne)
    {
        var m = Regex.Match(stderrLigne, @"code=(-?\d+)", RegexOptions.IgnoreCase);
        var code = m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var c)
            ? c
            : (int?)null;

        var sb = new StringBuilder();
        sb.Append("Le port TCP répond, mais le SDK ZKTeco n’a pas pu établir la session avec le terminal. ");
        sb.Append($"Paramètres utilisés : {ip}:{port}. ");
        if (code.HasValue)
        {
            sb.Append("Code SDK : ").Append(code.Value).Append(". ");
            sb.Append(DescriptionCodeSdkZk(code.Value));
        }
        else
        {
            sb.Append(
                "Vérifiez le numéro de machine (souvent 1 sur l’appareil), que le modèle est bien compatible avec le SDK « EU » (ZKEUEmKeeperNet), et la configuration réseau du terminal dans son menu administrateur.");
        }

        return sb.ToString().Trim();
    }

    private static string DescriptionCodeSdkZk(int code) =>
        code switch
        {
            -201 => "Souvent lié au réseau ou aux paramètres du terminal ; vérifiez la passerelle, l’absence de filtrage IP sur l’appareil et réessayez après redémarrage du terminal.",
            -2 => "Souvent un délai ou une erreur de communication ; testez un ping vers l’IP et un câble réseau direct si possible.",
            -1 => "Erreur générique du SDK ; contrôlez IP, port et numéro de machine.",
            _ => "Consultez la documentation ZKTeco pour ce code ou testez avec l’outil officiel du fabricant sur le même PC.",
        };

    private sealed class ZkPullRowDto
    {
        public string? p { get; set; }
        public string? t { get; set; }
    }

    public sealed class ZkUserDto
    {
        public string Id { get; set; } = "";
        public string Nom { get; set; } = "";
        public int Privilege { get; set; }
        public bool Actif { get; set; }
    }

    public static IReadOnlyList<ZkUserDto> LireUtilisateurs(string ip, int port, int machineId, int commPassword = 0)
    {
        var exePath = ResoudreCheminExeWorker();
        if (exePath == null)
            throw new FileNotFoundException(
                "ZktecoPullWorker.exe est introuvable (dossier « ZktecoPullWorker » sous l’application ou ancien emplacement à la racine).");

        VerifierPortTcp(ip.Trim(), port);

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        psi.ArgumentList.Add("--users");
        psi.ArgumentList.Add(ip.Trim());
        psi.ArgumentList.Add(port.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add(machineId.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add(commPassword.ToString(CultureInfo.InvariantCulture));

        using var process = new Process { StartInfo = psi };
        process.Start();
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(TimeoutMs))
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException("La lecture des utilisateurs ZKTeco a dépassé la durée maximale.");
        }

        if (process.ExitCode != 0)
        {
            var msg = string.IsNullOrWhiteSpace(stderr) ? "Échec de lecture des utilisateurs terminal." : stderr.Trim();
            throw new InvalidOperationException(msg);
        }

        stdout = stdout.Trim();
        if (string.IsNullOrEmpty(stdout) || stdout == "[]")
            return Array.Empty<ZkUserDto>();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var rows = JsonSerializer.Deserialize<List<ZkUserPullDto>>(stdout, options) ?? new List<ZkUserPullDto>();
        return rows
            .Where(x => !string.IsNullOrWhiteSpace(x.id))
            .Select(x => new ZkUserDto
            {
                Id = x.id!.Trim(),
                Nom = x.n?.Trim() ?? "",
                Privilege = x.p,
                Actif = x.e
            })
            .ToList();
    }

    private sealed class ZkUserPullDto
    {
        public string? id { get; set; }
        public string? n { get; set; }
        public int p { get; set; }
        public bool e { get; set; }
    }
}
