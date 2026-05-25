using System.Collections.ObjectModel;
using System.Linq;
using System.Globalization;
using System.Media;
using System.Windows;
using System.Windows.Threading;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>Détail d'un pointage reçu en direct (terminal).</summary>
public sealed class PointageRecuEventArgs
{
    public required string NomComplet { get; init; }
    public required string Matricule { get; init; }
    public required string Moment { get; init; }
    public required string HeureAffichage { get; init; }
    public required bool EstRetard { get; init; }
    public required bool ReconnuMelody { get; init; }
    public required string CodePin { get; init; }
    public DateTime HorodatageLocal { get; init; }
}

/// <summary>Toast affiché dans le panneau pointage.</summary>
public sealed class PointageToastItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Initiales { get; init; } = "?";
    public string NomComplet { get; init; } = "";
    public string SousTitre { get; init; } = "";
    public string Moment { get; init; } = "";
    public string HeureAffichage { get; init; } = "";
    public bool EstRetard { get; init; }
    public bool ReconnuMelody { get; init; }
    public string CouleurAccent { get; init; } = "#43A047";
}

/// <summary>
/// Notifications live (son, badge menu, toasts) pour chaque nouveau pointage terminal.
/// Déduplication globale partagée entre la synchro ZK et le panneau présence.
/// </summary>
public static class PointageLiveNotificationService
{
    private static readonly HashSet<string> KeysVus = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> CompteurJourParUser = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, DateTime> DernierPointageAccepteParUser = new(StringComparer.OrdinalIgnoreCase);
    private static int _nonLus;
    private static string _suffixeJourCourant = "";
    private static bool _amorcageJourEffectue;

    public static event Action<PointageRecuEventArgs>? PointageRecu;
    public static event Action? EtatChange;

    public static ObservableCollection<PointageToastItem> Toasts { get; } = new();

    public static bool SonActif { get; set; } = true;

    public static int NonLus
    {
        get => _nonLus;
        private set
        {
            if (_nonLus == value) return;
            _nonLus = Math.Max(0, value);
            EtatChange?.Invoke();
        }
    }

    public static string BadgeLibelle => _nonLus > 9 ? "9+" : _nonLus.ToString(CultureInfo.InvariantCulture);

    public static bool AfficherBadge => _nonLus > 0;

    public static void ReinitialiserBadge() => NonLus = 0;

    /// <summary>Marque des pointages déjà connus sans notification (ouverture écran / reprise journée).</summary>
    public static void MarquerCommeVusSansNotifier(IReadOnlyList<(string CodePin, DateTime Horodatage)>? logs)
    {
        if (logs == null || logs.Count == 0)
            return;

        AssurerJourCourant();
        foreach (var (codePin, horodatage) in logs)
        {
            var local = NormaliserHorodatage(horodatage);
            if (local.Date != DateTime.Today)
                continue;
            KeysVus.Add($"{NormaliserCle(codePin)}|{local:O}");
        }
    }

    /// <summary>Traite les journaux lus et notifie uniquement les pointages jamais vus.</summary>
    public static int TraiterNouveauxLogs(IReadOnlyList<(string CodePin, DateTime Horodatage)>? logs)
    {
        if (logs == null || logs.Count == 0)
            return 0;

        AssurerJourCourant();
        if (!_amorcageJourEffectue)
        {
            MarquerCommeVusSansNotifier(logs);
            _amorcageJourEffectue = true;
            return 0;
        }

        var regles = ChargerRegles();
        using var db = new PaieDbContext();
        var map = ConstruireMapEmployes(db);
        var publies = 0;

        foreach (var (codePin, horodatage) in logs.OrderBy(x => x.Horodatage))
        {
            var local = NormaliserHorodatage(horodatage);
            if (local.Date != DateTime.Today)
                continue;

            var key = $"{NormaliserCle(codePin)}|{local:O}";
            if (!KeysVus.Add(key))
                continue;

            var moment = DeterminerMoment(codePin, local, regles);
            if (moment == "Lecture en double (ignorée)")
                continue;

            var emp = ResoudreEmploye(map, codePin);
            var estRetard = moment.StartsWith("Entrée", StringComparison.OrdinalIgnoreCase)
                            && moment.Contains("retard", StringComparison.OrdinalIgnoreCase);

            var args = new PointageRecuEventArgs
            {
                CodePin = codePin.Trim(),
                HorodatageLocal = local,
                NomComplet = emp != null
                    ? $"{emp.Nom} {emp.Postnom} {emp.Prenom}".Trim()
                    : $"ID terminal {codePin.Trim()}",
                Matricule = emp?.Matricule ?? "—",
                Moment = moment,
                HeureAffichage = local.ToString("HH:mm"),
                EstRetard = estRetard,
                ReconnuMelody = emp != null
            };

            Publier(args);
            publies++;
        }

        return publies;
    }

    private static void Publier(PointageRecuEventArgs args)
    {
        NonLus++;
        PointageRecu?.Invoke(args);
        AjouterToast(args);
        if (SonActif)
            JouerSon(args.EstRetard);

        AppNotificationService.Afficher(
            $"{args.NomComplet} — {args.Moment} à {args.HeureAffichage}",
            args.EstRetard ? NotificationKind.Warning : NotificationKind.Success);
    }

    private static void AjouterToast(PointageRecuEventArgs args)
    {
        var accent = args.EstRetard ? "#E53935"
            : args.Moment.Contains("Sortie", StringComparison.OrdinalIgnoreCase) ? "#8E24AA"
            : args.Moment.Contains("pause", StringComparison.OrdinalIgnoreCase) ? "#FB8C00"
            : "#43A047";

        var toast = new PointageToastItem
        {
            Initiales = ExtraireInitiales(args.NomComplet),
            NomComplet = args.NomComplet,
            SousTitre = args.ReconnuMelody ? args.Matricule : "Non reconnu dans Melody",
            Moment = args.Moment,
            HeureAffichage = args.HeureAffichage,
            EstRetard = args.EstRetard,
            ReconnuMelody = args.ReconnuMelody,
            CouleurAccent = accent
        };

        void AjouterSurUi()
        {
            Toasts.Insert(0, toast);
            while (Toasts.Count > 6)
                Toasts.RemoveAt(Toasts.Count - 1);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Toasts.Remove(toast);
            };
            timer.Start();
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
            dispatcher.Invoke(AjouterSurUi);
        else
            AjouterSurUi();
    }

    private static void JouerSon(bool estRetard)
    {
        try
        {
            if (estRetard)
                SystemSounds.Hand.Play();
            else
                SystemSounds.Asterisk.Play();
        }
        catch
        {
            // Pas de son disponible sur certaines configurations.
        }
    }

    private static void AssurerJourCourant()
    {
        var suffixe = DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        if (suffixe == _suffixeJourCourant)
            return;

        _suffixeJourCourant = suffixe;
        KeysVus.Clear();
        CompteurJourParUser.Clear();
        DernierPointageAccepteParUser.Clear();
        _amorcageJourEffectue = false;
    }

    private static LtServicesRegles ChargerRegles()
    {
        using var db = new PaieDbContext();
        return LtServicesReglesProvider.ChargerDepuisDb(db);
    }

    private static string DeterminerMoment(string codePin, DateTime local, LtServicesRegles regles)
    {
        var key = $"{NormaliserCle(codePin)}|{local:yyyyMMdd}";
        if (DernierPointageAccepteParUser.TryGetValue(key, out var dernier)
            && local - dernier < PointagesNettoyageHelper.IntervalleAntiDoublon)
            return "Lecture en double (ignorée)";

        if (!CompteurJourParUser.TryGetValue(key, out var count))
            count = 0;

        var t = local.TimeOfDay;
        var entreeLabel = t <= regles.HeureLimiteTolerance ? "Entrée" : "Entrée (retard)";

        string moment;
        if (regles.UtiliseDeuxPointages)
        {
            moment = count switch
            {
                0 => entreeLabel,
                1 => "Sortie",
                _ => "Pointage supplémentaire"
            };
        }
        else if (regles.UtiliseTroisPointages)
        {
            moment = count switch
            {
                0 => entreeLabel,
                1 => "Pause",
                2 => "Sortie",
                _ => "Pointage supplémentaire"
            };
        }
        else
        {
            moment = count switch
            {
                0 => entreeLabel,
                1 => "Début pause",
                2 => "Fin pause",
                3 => "Sortie",
                _ => "Pointage supplémentaire"
            };
        }

        CompteurJourParUser[key] = count + 1;
        DernierPointageAccepteParUser[key] = local;
        return moment;
    }

    private static Dictionary<string, Employe> ConstruireMapEmployes(PaieDbContext db)
    {
        var map = new Dictionary<string, Employe>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in db.Employes.AsNoTracking().Include(x => x.Departement).ToList())
        {
            void AjouterCle(string? val)
            {
                if (string.IsNullOrWhiteSpace(val)) return;
                var cle = NormaliserCle(val);
                if (!map.ContainsKey(cle)) map[cle] = e;
                var digits = NormaliserDigits(val);
                if (!string.IsNullOrWhiteSpace(digits) && !map.ContainsKey(digits))
                    map[digits] = e;
            }

            AjouterCle(e.ZkUserId);
            AjouterCle(e.Matricule);
        }

        return map;
    }

    private static Employe? ResoudreEmploye(Dictionary<string, Employe> map, string codePin)
    {
        var cle = NormaliserCle(codePin);
        if (map.TryGetValue(cle, out var emp))
            return emp;
        var digits = NormaliserDigits(codePin);
        if (!string.IsNullOrWhiteSpace(digits) && map.TryGetValue(digits, out emp))
            return emp;
        return null;
    }

    private static string ExtraireInitiales(string nom)
    {
        if (string.IsNullOrWhiteSpace(nom))
            return "?";
        var parts = nom.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "?";
        if (parts.Length == 1)
            return parts[0].Length >= 2 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();
        return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
    }

    private static DateTime NormaliserHorodatage(DateTime dt) =>
        dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime()
        : dt.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(dt, DateTimeKind.Local)
        : dt;

    private static string NormaliserCle(string valeur) => valeur.Trim().ToUpperInvariant();

    private static string NormaliserDigits(string valeur) =>
        new string(valeur.Where(char.IsDigit).ToArray());
}
