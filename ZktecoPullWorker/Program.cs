using System.Globalization;
using System.Text.Json;
using ZkSoftwareEU;

namespace ZktecoPullWorker;

/// <summary>
/// Utilitaire lancé par MelodyPaieRDC.exe : le SDK ZKTeco sous .NET Framework évite
/// « Thread abort is not supported on this platform » (.NET 8 interdit Thread.Abort utilisé par la DLL).
/// Sortie stdout : JSON [{ "p":"matricule", "t":"2026-05-07T08:30:00" }, ...]
/// Codes retour : 0 OK, 1 erreur (détails stderr), 2 arguments invalides.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length >= 1 && string.Equals(args[0], "--users", StringComparison.OrdinalIgnoreCase))
                return LireUtilisateurs(args);

            if (args.Length < 3 ||
                !int.TryParse(args[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) ||
                !int.TryParse(args[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var machineId))
            {
                Console.Error.WriteLine("Usage: ZktecoPullWorker <ip> <port> <machineNumber> [commPassword]");
                return 2;
            }

            var ip = args[0].Trim();
            var rows = new List<RowDto>();
            var zk = new CZKEUEMNetClass();

            // Clé de communication (menu Connexion PC) : 000000 → 0. Optionnel 4e argument = mot de passe numérique.
            var commPwd = 0;
            if (args.Length >= 4 && int.TryParse(args[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPwd))
                commPwd = parsedPwd;
            zk.SetCommPassword(commPwd);

            if (!zk.Connect_Net(ip, port))
            {
                var code = 0;
                try
                {
                    zk.GetLastError(ref code);
                }
                catch
                {
                    /* SDK : méthode absente ou COM indisponible */
                }

                // Message analysé par MelodyPaieRDC (ZktecoPointageReader) pour guider l'utilisateur.
                Console.Error.WriteLine(code != 0 ? $"CONN_FAIL code={code}" : "CONN_FAIL");
                return 1;
            }

            zk.EnableDevice(machineId, false);
            try
            {
                zk.ReadGeneralLogData(machineId);
                LireSsr(zk, machineId, rows);
                if (rows.Count == 0)
                {
                    zk.ReadAllGLogData(machineId);
                    LireLegacy(zk, machineId, rows);
                }
            }
            finally
            {
                zk.EnableDevice(machineId, true);
            }

            zk.Disconnect();

            var json = JsonSerializer.Serialize(rows);
            Console.Out.WriteLine(json);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static int LireUtilisateurs(string[] args)
    {
        if (args.Length < 4 ||
            !int.TryParse(args[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) ||
            !int.TryParse(args[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var machineId))
        {
            Console.Error.WriteLine("Usage: ZktecoPullWorker --users <ip> <port> <machineNumber> [commPassword]");
            return 2;
        }

        var ip = args[1].Trim();
        var commPwd = 0;
        if (args.Length >= 5 && int.TryParse(args[4].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPwd))
            commPwd = parsedPwd;

        var users = new List<UserDto>();
        var zk = new CZKEUEMNetClass();
        zk.SetCommPassword(commPwd);
        if (!zk.Connect_Net(ip, port))
        {
            Console.Error.WriteLine("CONN_FAIL");
            return 1;
        }

        try
        {
            zk.EnableDevice(machineId, false);
            zk.ReadAllUserID(machineId);
            while (true)
            {
                var enroll = "";
                var name = "";
                var password = "";
                var privilege = 0;
                var enabled = false;
                if (!zk.SSR_GetAllUserInfo(machineId, ref enroll, ref name, ref password, ref privilege, ref enabled))
                    break;
                users.Add(new UserDto
                {
                    id = enroll?.Trim() ?? "",
                    n = name?.Trim() ?? "",
                    p = privilege,
                    e = enabled
                });
            }
        }
        finally
        {
            zk.EnableDevice(machineId, true);
            zk.Disconnect();
        }

        Console.Out.WriteLine(JsonSerializer.Serialize(users));
        return 0;
    }

    private static void LireSsr(CZKEUEMNetClass zk, int machineId, List<RowDto> rows)
    {
        while (true)
        {
            var enroll = "";
            int verify = 0, inoutMode = 0, y = 0, mo = 0, d = 0, h = 0, mi = 0, s = 0, work = 0;
            if (!zk.SSR_GetGeneralLogData(machineId, ref enroll, ref verify, ref inoutMode, ref y, ref mo, ref d, ref h,
                    ref mi, ref s, ref work))
                break;
            if (y < 2000)
                continue;
            try
            {
                var dt = new DateTime(y, mo, d, h, mi, s);
                rows.Add(new RowDto { p = enroll.Trim(), t = dt.ToString("o") });
            }
            catch
            {
                /* ignoré */
            }
        }
    }

    private static void LireLegacy(CZKEUEMNetClass zk, int machineId, List<RowDto> rows)
    {
        while (true)
        {
            int tMachine = 0, enroll = 0, eMachine = 0, verify = 0, inoutMode = 0, y = 0, mo = 0, d = 0, h = 0, mi = 0;
            if (!zk.GetGeneralLogData(machineId, ref tMachine, ref enroll, ref eMachine, ref verify, ref inoutMode,
                    ref y, ref mo, ref d, ref h, ref mi))
                break;
            if (y < 2000)
                continue;
            try
            {
                var dt = new DateTime(y, mo, d, h, mi, 0);
                rows.Add(new RowDto { p = enroll.ToString(CultureInfo.InvariantCulture), t = dt.ToString("o") });
            }
            catch
            {
                /* ignoré */
            }
        }
    }

    private sealed class RowDto
    {
        public string p { get; set; } = "";
        public string t { get; set; } = "";
    }

    private sealed class UserDto
    {
        public string id { get; set; } = "";
        public string n { get; set; } = "";
        public int p { get; set; }
        public bool e { get; set; }
    }
}
