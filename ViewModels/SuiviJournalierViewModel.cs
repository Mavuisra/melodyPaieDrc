using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.ViewModels;

/// <summary>Ligne de suivi pour un jour donné.</summary>
public class SuiviJournalierLigne : INotifyPropertyChanged
{
    private bool _suppressManualTracking;

    public DateTime Date { get; set; }

    private decimal _heuresPrestees;
    private string _typeJour = SuiviJournalier.TypeNormal;
    private string? _pointagesJson;
    private bool _heuresManuelles;

    public decimal HeuresPrestees
    {
        get => _heuresPrestees;
        set
        {
            if (_heuresPrestees == value) return;
            _heuresPrestees = value;
            if (!_suppressManualTracking)
                HeuresManuelles = true;
            OnPropertyChanged();
            OnPropertyChanged(nameof(JourCode));
            OnPropertyChanged(nameof(ModeCalculLibelle));
        }
    }

    public string TypeJour
    {
        get => _typeJour;
        set
        {
            if (_typeJour == value) return;
            _typeJour = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(JourCode));
            OnPropertyChanged(nameof(ModeCalculLibelle));
        }
    }

    /// <summary>JSON des horodatages — sert au recalcul automatique LTservices.</summary>
    public string? PointagesJson
    {
        get => _pointagesJson;
        set
        {
            if (_pointagesJson == value) return;
            _pointagesJson = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ModeCalculLibelle));
        }
    }

    public bool HeuresManuelles
    {
        get => _heuresManuelles;
        set
        {
            if (_heuresManuelles == value) return;
            _heuresManuelles = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ModeCalculLibelle));
        }
    }

    /// <summary>Indication du mode : calcul auto depuis pointages, saisie manuelle, ou défaut.</summary>
    public string ModeCalculLibelle
    {
        get
        {
            if (!string.IsNullOrEmpty(PointagesJson) && !HeuresManuelles)
                return "Auto (LT)";
            if (HeuresManuelles)
                return "Manuel";
            return "—";
        }
    }

    public string DateAffichage => Date.ToString("dd/MM/yyyy");
    public string JourSemaine => Date.ToString("dddd", new System.Globalization.CultureInfo("fr-FR"));
    public int JourCode => TypeJour == SuiviJournalier.TypeNormal && HeuresPrestees > 0m ? 1 : 0;

    /// <summary>Chargement initial ou rechargement depuis la base (sans marquer « manuel »).</summary>
    public void InitialiserDepuisDonneesBase(decimal heures, bool manuel, string? pointagesJson)
    {
        _suppressManualTracking = true;
        try
        {
            _heuresPrestees = heures;
            _heuresManuelles = manuel;
            _pointagesJson = pointagesJson;
            OnPropertyChanged(nameof(HeuresPrestees));
            OnPropertyChanged(nameof(HeuresManuelles));
            OnPropertyChanged(nameof(PointagesJson));
            OnPropertyChanged(nameof(JourCode));
            OnPropertyChanged(nameof(ModeCalculLibelle));
        }
        finally
        {
            _suppressManualTracking = false;
        }
    }

    /// <summary>Recalcul automatique depuis les horodatages enregistrés (réinitialise le mode manuel).</summary>
    public void AppliquerHeuresAutomatiques(decimal heuresCalculees)
    {
        _suppressManualTracking = true;
        try
        {
            _heuresManuelles = false;
            _heuresPrestees = heuresCalculees;
            OnPropertyChanged(nameof(HeuresPrestees));
            OnPropertyChanged(nameof(HeuresManuelles));
            OnPropertyChanged(nameof(JourCode));
            OnPropertyChanged(nameof(ModeCalculLibelle));
        }
        finally
        {
            _suppressManualTracking = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Ligne affichée dans la table de présence en direct.</summary>
public class PresencePointageLigne
{
    public string Jour { get; set; } = "";
    public string Heure { get; set; } = "";
    public string Minute { get; set; } = "";
    public string Moment { get; set; } = "";
    public string ZkUserId { get; set; } = "";
    public string Matricule { get; set; } = "";
    public string NomComplet { get; set; } = "";
    public string Departement { get; set; } = "";
    public string Statut { get; set; } = "";
    /// <summary>Horodatage local du pointage (pour filtre jour et calcul des durées).</summary>
    public DateTime HorodatageLocal { get; set; }
}

/// <summary>Synthèse présence du jour : une ligne par employé avec les moments clés.</summary>
public class PresenceEmployeSyntheseLigne
{
    public string Jour { get; set; } = "";
    public string Matricule { get; set; } = "";
    public string NomComplet { get; set; } = "";
    public string Departement { get; set; } = "";
    public string Entree { get; set; } = "—";
    public string DebutPause { get; set; } = "—";
    public string FinPause { get; set; } = "—";
    public string Sortie { get; set; } = "—";
    public string Autres { get; set; } = "—";
    public string Statut { get; set; } = "";
    public bool EstRetard { get; set; }
    public string IndicateurRetard { get; set; } = "À l'heure";
}

public class SuiviJournalierViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private readonly List<Employe> _sourceEmployes = new();
    private Employe? _employeSelectionne;
    private PeriodePaie? _periodeSelectionnee;
    private string _rechercheEmployeText = "";
    private DispatcherTimer? _presenceTimer;
    private string _presenceStatut = "Surveillance automatique au repos.";
    private bool _surveillanceAutomatiqueActive;
    /// <summary>1 pendant une synchro terminal — évite les chevauchements sans bloquer le thread UI entre deux awaits.</summary>
    private int _presenceCycleBusy;
    /// <summary>Intervalle entre deux cycles de synchro (léger pour le PC ; une seule lecture terminal par cycle via la file globale).</summary>
    private static readonly TimeSpan IntervalSurveillancePresence = TimeSpan.FromSeconds(3);
    private readonly HashSet<string> _presenceKeysVus = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<DateTime>> _historiquePointageParUserJour = new(StringComparer.OrdinalIgnoreCase);
    private string _zkConnexionResume = "";
    private DateTime? _zkDerniereSyncUtc;
    private DateTime _derniereMajUiSyncUtc;
    private string _presenceResumeDureesAujourdhui = "Aujourd’hui — aucun pointage pour le moment.";

    public SuiviJournalierViewModel(PaieDbContext db)
    {
        _db = db;
        Employes = new ObservableCollection<Employe>();
        PeriodesPaie = new ObservableCollection<PeriodePaie>();
        Lignes = new ObservableCollection<SuiviJournalierLigne>();
        EnregistrerCommand = new RelayCommand(_ => Enregistrer(), _ => EmployeSelectionne != null && PeriodeSelectionnee != null);
        ChargerEmployesCommand = new RelayCommand(_ => ChargerEmployes());
        ChargerPeriodesCommand = new RelayCommand(_ => ChargerPeriodes());
        RechercherEmployeCommand = new RelayCommand(_ => RechercherEmployes());
        ImporterUtilisateursTerminalCommand = new RelayCommand(_ => ImporterUtilisateursTerminal());
        ChargerLignesCommand = new RelayCommand(_ => ChargerLignes(), _ => EmployeSelectionne != null && PeriodeSelectionnee != null);
        RetablirCalculAutomatiqueCommand = new RelayCommand(_ => RetablirCalculAutomatique());
        ZktecoSynchronisationService.SynchroReussie += OnSynchroZkReussie;
        ZkTerminalParametresNotifier.ParametresModifies += OnZkTerminalParametresModifiesDepuisAutreEcran;
        RafraichirAffichageTerminalDepuisBase();
    }

    public ObservableCollection<Employe> Employes { get; }
    public ObservableCollection<PeriodePaie> PeriodesPaie { get; }
    public ObservableCollection<SuiviJournalierLigne> Lignes { get; }
    public ObservableCollection<PresencePointageLigne> PresencePointages { get; } = new();
    public ObservableCollection<PresenceEmployeSyntheseLigne> PresenceSyntheseEmployes { get; } = new();

    /// <summary>Résumé des durées du jour : dernier − premier pointage par employé (les pauses ne sont pas retranchées).</summary>
    public string PresenceResumeDureesAujourdhui
    {
        get => _presenceResumeDureesAujourdhui;
        private set
        {
            if (_presenceResumeDureesAujourdhui == value) return;
            _presenceResumeDureesAujourdhui = value ?? "";
            OnPropertyChanged(nameof(PresenceResumeDureesAujourdhui));
        }
    }

    public Employe? EmployeSelectionne
    {
        get => _employeSelectionne;
        set
        {
            _employeSelectionne = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MessageVide));
            (EnregistrerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ChargerLignesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            if (value != null && PeriodeSelectionnee != null)
                ChargerLignes();
        }
    }

    public PeriodePaie? PeriodeSelectionnee
    {
        get => _periodeSelectionnee;
        set
        {
            _periodeSelectionnee = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MessageVide));
            (EnregistrerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ChargerLignesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            if (value != null && EmployeSelectionne != null)
                ChargerLignes();
        }
    }

    public ICommand EnregistrerCommand { get; }
    public ICommand ChargerEmployesCommand { get; }
    public ICommand ChargerPeriodesCommand { get; }
    public ICommand RechercherEmployeCommand { get; }
    public ICommand ImporterUtilisateursTerminalCommand { get; }
    public ICommand ChargerLignesCommand { get; }

    /// <summary>Réapplique le calcul LTservices sur toutes les lignes qui ont des horodatages en base.</summary>
    public ICommand RetablirCalculAutomatiqueCommand { get; }

    /// <summary>Appelé après enregistrement réussi (message utilisateur ou fermeture fenêtre modale).</summary>
    public Action? OnSauvegardeReussie { get; set; }
    public Action<string>? OnErreur { get; set; }
    public Action<string>? OnMessageInformation { get; set; }

    public string RechercheEmployeText
    {
        get => _rechercheEmployeText;
        set { if (_rechercheEmployeText == value) return; _rechercheEmployeText = value ?? ""; OnPropertyChanged(); }
    }

    /// <summary>Vrai lorsque l’écran pointage est affiché : un minuteur poll le terminal de façon récurrente.</summary>
    public bool PresenceSurveillanceActive
    {
        get => _surveillanceAutomatiqueActive;
        private set { if (_surveillanceAutomatiqueActive == value) return; _surveillanceAutomatiqueActive = value; OnPropertyChanged(); }
    }

    /// <summary>Barre de progression uniquement pendant une synchro réelle (pas entre deux cycles).</summary>
    public bool PresenceSynchronisationEnCours => Volatile.Read(ref _presenceCycleBusy) != 0;

    private void NotifierPresenceSynchronisationEnCours() =>
        OnPropertyChanged(nameof(PresenceSynchronisationEnCours));

    public string PresenceStatut
    {
        get => _presenceStatut;
        private set { _presenceStatut = value; OnPropertyChanged(); }
    }

    /// <summary>Résumé réseau lu depuis Paramètres > ZKTeco (rechargé depuis la base).</summary>
    public string ZkConnexionResume
    {
        get => _zkConnexionResume;
        private set { if (_zkConnexionResume == value) return; _zkConnexionResume = value ?? ""; OnPropertyChanged(); }
    }

    public string ZkStatutSync =>
        !_zkDerniereSyncUtc.HasValue
            ? "Dernière synchro : —"
            : $"Dernière synchro : {_zkDerniereSyncUtc.Value.ToLocalTime():dd/MM/yyyy HH:mm:ss}";

    private void OnZkTerminalParametresModifiesDepuisAutreEcran(object? sender, EventArgs e)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher.CheckAccess() == true)
            RafraichirAffichageTerminalDepuisBase();
        else
            app?.Dispatcher.Invoke(RafraichirAffichageTerminalDepuisBase);
    }

    /// <summary>Relecture des paramètres terminal depuis la base (ex. après modification dans Paramètres).</summary>
    public void RafraichirAffichageTerminalDepuisBase()
    {
        var p = ZkTerminalParametresResolver.ObtenirParametresZkFresh(_db);
        if (p == null)
            return;

        _zkDerniereSyncUtc = p.ZkDerniereSyncUtc;
        ZkConnexionResume = ZkTerminalParametresResolver.FormaterResumeConnexion(p);
        OnPropertyChanged(nameof(ZkStatutSync));
    }

    private void OnSynchroZkReussie(DateTime _)
    {
        var now = DateTime.UtcNow;
        if ((now - _derniereMajUiSyncUtc).TotalSeconds < 2)
            return;

        _derniereMajUiSyncUtc = now;
        if (EmployeSelectionne != null && PeriodeSelectionnee != null)
            ChargerLignes();
        RafraichirAffichageTerminalDepuisBase();
    }

    public static string[] TypesJour => new[]
    {
        SuiviJournalier.TypeNormal,
        SuiviJournalier.TypeCongeCirconstance,
        SuiviJournalier.TypeMaladie,
        SuiviJournalier.TypePreavis
    };

    /// <summary>Message affiché quand aucune donnée (période ou employé manquant).</summary>
    public string MessageVide
    {
        get
        {
            if (Employes.Count == 0 && PeriodesPaie.Count == 0)
                return "Ajoutez d'abord des employés (menu Employés) et des périodes de paie (Paramètres → Périodes de paie).";
            if (Employes.Count == 0)
                return "Ajoutez d'abord des employés (menu Employés).";
            if (PeriodesPaie.Count == 0)
                return "Ajoutez d'abord des périodes de paie (Paramètres → Périodes de paie).";
            if (EmployeSelectionne == null || PeriodeSelectionnee == null)
                return "Sélectionnez une période et un employé.";
            return "";
        }
    }

    public void ChargerEmployes()
    {
        _sourceEmployes.Clear();
        _sourceEmployes.AddRange(_db.Employes.Include(x => x.Departement).OrderBy(x => x.Nom).ThenBy(x => x.Prenom));
        RechercherEmployes();
        OnPropertyChanged(nameof(MessageVide));
    }

    private void RechercherEmployes()
    {
        var filtre = (RechercheEmployeText ?? "").Trim().ToLowerInvariant();
        Employes.Clear();
        foreach (var e in _sourceEmployes)
        {
            if (!string.IsNullOrWhiteSpace(filtre))
            {
                var nomComplet = $"{e.Nom} {e.Postnom} {e.Prenom}".Trim().ToLowerInvariant();
                var matricule = (e.Matricule ?? "").ToLowerInvariant();
                if (!nomComplet.Contains(filtre) && !matricule.Contains(filtre))
                    continue;
            }
            Employes.Add(e);
        }

        if (EmployeSelectionne != null && !Employes.Any(x => x.Id == EmployeSelectionne.Id))
            EmployeSelectionne = null;

        OnPropertyChanged(nameof(MessageVide));
    }

    /// <summary>À appeler lorsque l’utilisateur affiche l’écran pointage (panneau visible).</summary>
    public void DemarrerSurveillancePresenceAutomatique()
    {
        if (_surveillanceAutomatiqueActive && _presenceTimer != null)
            return;

        PresenceSurveillanceActive = true;
        PresenceStatut = "Surveillance automatique — synchronisation du terminal…";

        _presenceTimer?.Stop();
        _presenceTimer = new DispatcherTimer { Interval = IntervalSurveillancePresence };
        _presenceTimer.Tick -= PresenceTimerOnTick;
        _presenceTimer.Tick += PresenceTimerOnTick;
        _presenceTimer.Start();

        _ = TraiterCyclePresenceAsync();
    }

    /// <summary>À appeler lorsque l’utilisateur quitte l’écran (visibilité ou fermeture) pour libérer le minuteur.</summary>
    public void ArreterSurveillancePresenceAutomatique()
    {
        PresenceSurveillanceActive = false;
        _presenceTimer?.Stop();
        _presenceTimer = null;
        PresenceStatut = "Surveillance arrêtée (écran pointage masqué).";
    }

    private void PresenceTimerOnTick(object? sender, EventArgs e)
    {
        if (!_surveillanceAutomatiqueActive || Volatile.Read(ref _presenceCycleBusy) != 0)
            return;

        _ = TraiterCyclePresenceAsync();
    }

    private async Task TraiterCyclePresenceAsync()
    {
        if (!_surveillanceAutomatiqueActive)
            return;

        if (Interlocked.CompareExchange(ref _presenceCycleBusy, 1, 0) != 0)
            return;

        PurgerHistoriquePresencePourNouveauJour();
        FiltrerPresenceAujourdhui();

        NotifierPresenceSynchronisationEnCours();
        try
        {
            if (!ZkTerminalParametresResolver.TryGetConnexion(_db, out _, out _, out _, out _, out _, out var errPre))
            {
                PresenceStatut = errPre ?? "Configurez le terminal dans Paramètres > ZKTeco.";
                return;
            }

            PresenceStatut = "Synchronisation du terminal…";
            var (ok, logs, err) = await ZktecoSynchronisationService.TrySynchroniserAvecLogsAsync().ConfigureAwait(true);
            if (!ok)
            {
                PresenceStatut = string.IsNullOrWhiteSpace(err) ? "Échec de la synchronisation." : err;
                return;
            }

            RafraichirAffichageTerminalDepuisBase();
            if (PeriodeSelectionnee != null)
                ChargerLignes();

            var nouveaux = FiltrerNouveauxPourPresence(logs);
            if (nouveaux.Count > 0)
            {
                var ajoutes = AjouterPointagesPresence(nouveaux);
                PresenceStatut = ajoutes > 0
                    ? $"{ajoutes} pointage(s) détecté(s) — sessions attribuées automatiquement"
                    : "Surveillance active — en attente de pointage";
            }
            else
            {
                PresenceStatut = "Surveillance active — en attente de pointage";
            }
        }
        finally
        {
            Interlocked.Exchange(ref _presenceCycleBusy, 0);
            NotifierPresenceSynchronisationEnCours();
        }
    }

    private List<(string CodePin, DateTime Horodatage)> FiltrerNouveauxPourPresence(
        IReadOnlyList<(string CodePin, DateTime Horodatage)>? logs)
    {
        var nouveaux = new List<(string CodePin, DateTime Horodatage)>();
        if (logs == null || logs.Count == 0)
            return nouveaux;

        foreach (var l in logs.OrderBy(x => x.Horodatage))
        {
            var key = $"{NormaliserCleLocal(l.CodePin)}|{l.Horodatage:O}";
            if (_presenceKeysVus.Contains(key))
                continue;
            _presenceKeysVus.Add(key);
            nouveaux.Add(l);
        }

        return nouveaux;
    }

    private int AjouterPointagesPresence(IEnumerable<(string CodePin, DateTime Horodatage)> pointages)
    {
        var ajoutes = 0;
        var reglesLt = LtServicesReglesProvider.ChargerDepuisDb(_db);
        var mapEmployes = ConstruireMapEmployesPourPresence();
        foreach (var p in pointages.OrderBy(x => x.Horodatage))
        {
            var horodatageLocal = NormaliserHorodatageLocal(p.Horodatage);
            var moment = DeterminerMomentPointageParIntervalle(p.CodePin, horodatageLocal, reglesLt);

            var cle = NormaliserCleLocal(p.CodePin);
            var digits = NormaliserDigitsLocal(p.CodePin);
            mapEmployes.TryGetValue(cle, out var emp);
            if (emp == null && !string.IsNullOrWhiteSpace(digits))
                mapEmployes.TryGetValue(digits, out emp);

            var ligne = new PresencePointageLigne
            {
                Jour = horodatageLocal.ToString("dd/MM/yyyy"),
                Heure = horodatageLocal.ToString("HH"),
                Minute = horodatageLocal.ToString("mm"),
                Moment = moment,
                ZkUserId = p.CodePin,
                Matricule = emp?.Matricule ?? "—",
                NomComplet = emp == null ? "Non attribué" : $"{emp.Nom} {emp.Postnom} {emp.Prenom}".Trim(),
                Departement = emp?.Departement?.NomDepartement ?? "—",
                Statut = emp == null ? "Non reconnu Melody" : "Reconnu Melody",
                HorodatageLocal = horodatageLocal
            };
            PresencePointages.Insert(0, ligne);
            ajoutes++;
        }

        FiltrerPresenceAujourdhui();

        return ajoutes;
    }

    private static bool EstHorodatageUtilisable(DateTime h) =>
        h != default && h.Year > 2000;

    private void PurgerHistoriquePresencePourNouveauJour()
    {
        var suffixeJour = "|" + DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var cles = _historiquePointageParUserJour.Keys.Where(k => !k.EndsWith(suffixeJour, StringComparison.Ordinal)).ToList();
        foreach (var c in cles)
            _historiquePointageParUserJour.Remove(c);
    }

    /// <summary>Ne conserve que les pointages du jour civil courant et met à jour le résumé des durées.</summary>
    private void FiltrerPresenceAujourdhui()
    {
        var aujourdhui = DateTime.Today.Date;
        for (var i = PresencePointages.Count - 1; i >= 0; i--)
        {
            var r = PresencePointages[i];
            DateTime jourLigne;
            if (EstHorodatageUtilisable(r.HorodatageLocal))
                jourLigne = r.HorodatageLocal.Date;
            else if (!DateTime.TryParseExact(r.Jour, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                PresencePointages.RemoveAt(i);
                continue;
            }
            else
                jourLigne = parsed.Date;

            if (jourLigne != aujourdhui)
                PresencePointages.RemoveAt(i);
        }

        RecalculerResumeDureesAujourdhui();
        RecalculerSynthesePresenceEmployes();

        while (PresencePointages.Count > 200)
            PresencePointages.RemoveAt(PresencePointages.Count - 1);
    }

    private void RecalculerSynthesePresenceEmployes()
    {
        PresenceSyntheseEmployes.Clear();
        if (PresencePointages.Count == 0)
            return;

        var lignesAujourdhui = PresencePointages
            .Where(r => EstHorodatageUtilisable(r.HorodatageLocal) && r.HorodatageLocal.Date == DateTime.Today)
            .ToList();

        static string HeureMin(DateTime dt) => dt.ToString("HH:mm");

        foreach (var grp in lignesAujourdhui
                     .GroupBy(x => (x.Matricule, x.NomComplet, x.Departement, x.Statut))
                     .OrderBy(g => g.Key.NomComplet, StringComparer.CurrentCultureIgnoreCase))
        {
            var ordonnes = grp.OrderBy(x => x.HorodatageLocal).ToList();
            var momentsUtiles = ordonnes.Where(x =>
                    x.Moment == "Entrée" ||
                    x.Moment == "Entrée (retard)" ||
                    x.Moment == "Début pause" ||
                    x.Moment == "Fin pause" ||
                    x.Moment == "Sortie")
                .ToList();

            var entree = momentsUtiles.FirstOrDefault(x => x.Moment == "Entrée" || x.Moment == "Entrée (retard)");
            var debutPause = momentsUtiles.FirstOrDefault(x => x.Moment == "Début pause");
            var finPause = momentsUtiles.FirstOrDefault(x => x.Moment == "Fin pause");
            var sortie = momentsUtiles.FirstOrDefault(x => x.Moment == "Sortie");
            var estRetard = entree?.Moment == "Entrée (retard)";

            var autres = ordonnes
                .Where(x =>
                    x.Moment == "Pointage supplémentaire" ||
                    x.Moment == "Inconnu")
                .Select(x => HeureMin(x.HorodatageLocal))
                .ToList();

            PresenceSyntheseEmployes.Add(new PresenceEmployeSyntheseLigne
            {
                Jour = DateTime.Today.ToString("dd/MM/yyyy"),
                Matricule = string.IsNullOrWhiteSpace(grp.Key.Matricule) ? "—" : grp.Key.Matricule,
                NomComplet = string.IsNullOrWhiteSpace(grp.Key.NomComplet) ? "—" : grp.Key.NomComplet,
                Departement = string.IsNullOrWhiteSpace(grp.Key.Departement) ? "—" : grp.Key.Departement,
                Entree = entree == null ? "—" : HeureMin(entree.HorodatageLocal),
                DebutPause = debutPause == null ? "—" : HeureMin(debutPause.HorodatageLocal),
                FinPause = finPause == null ? "—" : HeureMin(finPause.HorodatageLocal),
                Sortie = sortie == null ? "—" : HeureMin(sortie.HorodatageLocal),
                Autres = autres.Count == 0 ? "—" : string.Join(", ", autres),
                Statut = string.IsNullOrWhiteSpace(grp.Key.Statut) ? "—" : grp.Key.Statut,
                EstRetard = estRetard,
                IndicateurRetard = estRetard ? "En retard" : "À l'heure"
            });
        }
    }

    private void RecalculerResumeDureesAujourdhui()
    {
        var aujourdhui = DateTime.Today.Date;
        var lignes = PresencePointages.Where(r =>
        {
            if (EstHorodatageUtilisable(r.HorodatageLocal))
                return r.HorodatageLocal.Date == aujourdhui;
            return DateTime.TryParseExact(r.Jour, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) && d.Date == aujourdhui;
        }).ToList();

        if (lignes.Count == 0)
        {
            PresenceResumeDureesAujourdhui = "Aujourd’hui — aucun pointage pour le moment.";
            return;
        }

        static DateTime? ParserHorodatage(PresencePointageLigne r)
        {
            if (EstHorodatageUtilisable(r.HorodatageLocal))
                return r.HorodatageLocal;
            if (DateTime.TryParseExact($"{r.Jour} {r.Heure}:{r.Minute}", "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;
            return null;
        }

        var parties = new List<string>();
        foreach (var grp in lignes.GroupBy(x => (x.Matricule, x.ZkUserId)))
        {
            var horaires = grp.Select(ParserHorodatage).Where(x => x.HasValue).Select(x => x!.Value).OrderBy(x => x).ToList();
            if (horaires.Count == 0)
                continue;

            var nom = grp.First().NomComplet;
            var mat = grp.Key.Matricule;
            if (horaires.Count == 1)
            {
                parties.Add($"{nom} ({mat}) — 1 pointage");
                continue;
            }

            var delta = horaires[^1] - horaires[0];
            var totalMin = (int)Math.Round(delta.TotalMinutes);
            if (totalMin < 0)
                continue;
            var hh = totalMin / 60;
            var mm = totalMin % 60;
            parties.Add($"{nom} ({mat}) — {hh}h{mm:00}");
        }

        PresenceResumeDureesAujourdhui =
            "Durées du jour : écart entre dernier et premier pointage (les pauses ne sont pas déduites de cet écart) — "
            + string.Join(" · ", parties);
    }

    private string DeterminerMomentPointageParIntervalle(string codePin, DateTime dateHeure, LtServicesRegles reglesLt)
    {
        var local = NormaliserHorodatageLocal(dateHeure);
        var t = local.TimeOfDay;
        var key = $"{NormaliserCleLocal(codePin)}|{local:yyyyMMdd}";
        if (!_historiquePointageParUserJour.TryGetValue(key, out var logsJour))
        {
            logsJour = new List<DateTime>();
            _historiquePointageParUserJour[key] = logsJour;
        }

        string moment;
        if (logsJour.Count == 0)
        {
            moment = t <= reglesLt.HeureLimiteTolerance ? "Entrée" : "Entrée (retard)";
        }
        else if (logsJour.Count == 1)
        {
            // 2e pointage de la journée : on force "Début pause"
            // pour éviter les doublons incohérents de moment.
            moment = "Début pause";
        }
        else if (logsJour.Count == 2)
        {
            // 3e pointage de la journée : "Fin pause"
            moment = "Fin pause";
        }
        else if (logsJour.Count == 3)
        {
            // 4e pointage de la journée : "Sortie"
            moment = "Sortie";
        }
        else
        {
            moment = "Pointage supplémentaire";
        }

        logsJour.Add(dateHeure);
        return moment;
    }

    private static DateTime NormaliserHorodatageLocal(DateTime dt)
    {
        if (dt.Kind == DateTimeKind.Utc)
            return dt.ToLocalTime();
        if (dt.Kind == DateTimeKind.Unspecified)
            return DateTime.SpecifyKind(dt, DateTimeKind.Local);
        return dt;
    }

    private Dictionary<string, Employe> ConstruireMapEmployesPourPresence()
    {
        var map = new Dictionary<string, Employe>(StringComparer.OrdinalIgnoreCase);
        var employes = _db.Employes
            .AsNoTracking()
            .Include(e => e.Departement)
            .ToList();
        foreach (var e in employes)
        {
            var zkId = e.ZkUserId?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(zkId))
            {
                var zkCle = NormaliserCleLocal(zkId);
                if (!map.ContainsKey(zkCle)) map.Add(zkCle, e);
                var zkDigits = NormaliserDigitsLocal(zkId);
                if (!string.IsNullOrWhiteSpace(zkDigits) && !map.ContainsKey(zkDigits))
                    map.Add(zkDigits, e);
            }

            var mat = e.Matricule?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(mat))
                continue;
            var matCle = NormaliserCleLocal(mat);
            if (!map.ContainsKey(matCle)) map.Add(matCle, e);
            var matDigits = NormaliserDigitsLocal(mat);
            if (!string.IsNullOrWhiteSpace(matDigits) && !map.ContainsKey(matDigits))
                map.Add(matDigits, e);
        }

        return map;
    }

    private void ImporterUtilisateursTerminal()
    {
        try
        {
            if (!ZkTerminalParametresResolver.TryGetConnexion(_db, out var ip, out var port, out var machine, out _, out var commPwd, out var err))
            {
                OnErreur?.Invoke(err ?? "Paramètres terminal invalides.");
                return;
            }

            var users = ZktecoPointageReader.LireUtilisateurs(ip, port, machine, commPwd);
            var map = ConstruireMapCorrespondanceEmployes();

            var reconnus = 0;
            var inconnus = new List<string>();
            foreach (var u in users)
            {
                var id = NormaliserCleLocal(u.Id);
                var idDigits = NormaliserDigitsLocal(u.Id);
                if (map.ContainsKey(id) || (!string.IsNullOrWhiteSpace(idDigits) && map.ContainsKey(idDigits)))
                    reconnus++;
                else
                    inconnus.Add(string.IsNullOrWhiteSpace(u.Nom) ? u.Id : $"{u.Id} ({u.Nom})");
            }

            var resume = $"Utilisateurs terminal lus : {users.Count}. Reconnus dans Melody : {reconnus}. Non reconnus : {inconnus.Count}.";
            if (inconnus.Count > 0)
                resume += $"{Environment.NewLine}Exemples non reconnus : {string.Join(", ", inconnus.Take(8))}";
            OnMessageInformation?.Invoke(resume);
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke(ex.Message);
        }
    }

    private Dictionary<string, int> ConstruireMapCorrespondanceEmployes()
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var employes = _db.Employes.AsNoTracking().ToList();
        foreach (var e in employes)
        {
            var zkId = e.ZkUserId?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(zkId))
            {
                var zkCle = NormaliserCleLocal(zkId);
                if (!map.ContainsKey(zkCle)) map.Add(zkCle, e.Id);

                var zkDigits = NormaliserDigitsLocal(zkId);
                if (!string.IsNullOrWhiteSpace(zkDigits) && !map.ContainsKey(zkDigits))
                    map.Add(zkDigits, e.Id);
            }

            var mat = e.Matricule?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(mat))
                continue;

            var matCle = NormaliserCleLocal(mat);
            if (!map.ContainsKey(matCle)) map.Add(matCle, e.Id);

            var matDigits = NormaliserDigitsLocal(mat);
            if (!string.IsNullOrWhiteSpace(matDigits) && !map.ContainsKey(matDigits))
                map.Add(matDigits, e.Id);
        }

        return map;
    }

    private static string NormaliserCleLocal(string valeur) =>
        (valeur ?? "").Trim().Replace(" ", "").ToUpperInvariant();

    private static string NormaliserDigitsLocal(string valeur)
    {
        var digits = new string((valeur ?? "").Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(digits)) return "";
        var sansZeros = digits.TrimStart('0');
        return string.IsNullOrEmpty(sansZeros) ? "0" : sansZeros;
    }

    public void ChargerPeriodes()
    {
        PeriodesPaie.Clear();
        foreach (var p in _db.PeriodesPaie.OrderByDescending(x => x.Annee).ThenByDescending(x => x.Mois))
            PeriodesPaie.Add(p);
        OnPropertyChanged(nameof(MessageVide));
    }

    /// <summary>Sélectionne la première période et le premier employé pour afficher la grille de saisie.</summary>
    public void SelectionnerPremiersParDefaut()
    {
        if (PeriodesPaie.Count > 0 && PeriodeSelectionnee == null)
            PeriodeSelectionnee = PeriodesPaie[0];
        if (Employes.Count > 0 && EmployeSelectionne == null)
            EmployeSelectionne = Employes[0];
    }

    public void ChargerLignes()
    {
        Lignes.Clear();
        if (EmployeSelectionne == null || PeriodeSelectionnee == null) return;

        try
        {
            var reglesLt = LtServicesReglesProvider.ChargerDepuisDb(_db);
            var dateDebut = new DateTime(PeriodeSelectionnee.Annee, PeriodeSelectionnee.Mois, 1).Date;
            var dateFin = dateDebut.AddMonths(1).AddDays(-1).Date; // Dernier jour du mois inclus
            var employeId = EmployeSelectionne.Id;

            var existants = _db.SuivisJournaliers
                .Where(s => s.EmployeId == employeId && s.Date >= dateDebut && s.Date <= dateFin)
                .ToDictionary(s => s.Date.Date);

            var calendrier = _db.JoursTravailCalendrier
                .Where(j => j.Annee == PeriodeSelectionnee.Annee && j.DateJour >= dateDebut && j.DateJour <= dateFin)
                .ToDictionary(j => j.DateJour.Date);

            // Si au moins un samedi est marqué "Ouvre", on considère un cycle 6 jours.
            var semaineSixJours = calendrier.Any(kvp =>
                kvp.Key.DayOfWeek == DayOfWeek.Saturday &&
                string.Equals(kvp.Value.TypeJour, "Ouvre", StringComparison.OrdinalIgnoreCase));

            for (var d = dateDebut; d <= dateFin; d = d.AddDays(1))
            {
                var existant = existants.GetValueOrDefault(d);
                var ligne = new SuiviJournalierLigne { Date = d };
                ligne.TypeJour = NormaliserTypeJour(existant?.TypeJour);

                if (ligne.TypeJour == SuiviJournalier.TypeNormal && existant != null &&
                    !string.IsNullOrEmpty(existant.PointagesJson) && !existant.HeuresManuelles)
                {
                    var h = PointagesJournalierSerializer.CalculerHeuresLt(existant.PointagesJson, d, reglesLt);
                    ligne.InitialiserDepuisDonneesBase(h, false, existant.PointagesJson);
                }
                else if (existant != null)
                {
                    ligne.InitialiserDepuisDonneesBase(existant.HeuresPrestees, existant.HeuresManuelles, existant.PointagesJson);
                }
                else
                {
                    var heuresParDefaut = SuiviJournalierGrilleHelper.DeterminerHeuresParDefaut(d, semaineSixJours, calendrier);
                    ligne.InitialiserDepuisDonneesBase(heuresParDefaut, false, null);
                }

                Lignes.Add(ligne);
            }
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke($"Erreur chargement : {ex.Message}");
        }
    }

    private static string NormaliserTypeJour(string? typeJour)
    {
        if (string.IsNullOrWhiteSpace(typeJour))
            return SuiviJournalier.TypeNormal;

        return typeJour.Trim() switch
        {
            SuiviJournalier.TypeNormal => SuiviJournalier.TypeNormal,
            SuiviJournalier.TypeCongeCirconstance => SuiviJournalier.TypeCongeCirconstance,
            SuiviJournalier.TypeMaladie => SuiviJournalier.TypeMaladie,
            SuiviJournalier.TypePreavis => SuiviJournalier.TypePreavis,
            // Compatibilité des anciennes valeurs déjà enregistrées
            "Absence justifiée" => SuiviJournalier.TypeCongeCirconstance,
            "Absence non justifiée" => SuiviJournalier.TypeNormal,
            "Malade" => SuiviJournalier.TypeMaladie,
            _ => SuiviJournalier.TypeNormal
        };
    }

    private void Enregistrer()
    {
        if (EmployeSelectionne == null || PeriodeSelectionnee == null) return;
        try
        {
            var employeId = EmployeSelectionne.Id;
            var dateDebut = new DateTime(PeriodeSelectionnee.Annee, PeriodeSelectionnee.Mois, 1).Date;
            var dateFin = dateDebut.AddMonths(1).AddDays(-1).Date; // Jusqu'à la fin du mois inclus

            var existants = _db.SuivisJournaliers
                .Where(s => s.EmployeId == employeId && s.Date >= dateDebut && s.Date <= dateFin)
                .ToList();

            foreach (var ligne in Lignes)
            {
                var existant = existants.FirstOrDefault(x => x.Date.Date == ligne.Date.Date);
                // Heures effectives : Préavis = 0h, Normal = saisie utilisateur, autres types = journée standard (8h).
                var heures = ligne.TypeJour switch
                {
                    var t when t == SuiviJournalier.TypePreavis => 0m,
                    var t when t == SuiviJournalier.TypeNormal => Math.Max(0, Math.Min(24, ligne.HeuresPrestees)),
                    _ => 8m
                };

                if (existant != null)
                {
                    existant.HeuresPrestees = heures;
                    existant.TypeJour = ligne.TypeJour;
                    existant.PointagesJson = ligne.PointagesJson;
                    existant.HeuresManuelles = ligne.HeuresManuelles;
                }
                else
                {
                    _db.SuivisJournaliers.Add(new SuiviJournalier
                    {
                        EmployeId = employeId,
                        Date = ligne.Date.Date, // Date normalisée (sans heure)
                        HeuresPrestees = heures,
                        TypeJour = ligne.TypeJour,
                        PointagesJson = ligne.PointagesJson,
                        HeuresManuelles = ligne.HeuresManuelles
                    });
                }
            }

            _db.SaveChanges();
            OnSauvegardeReussie?.Invoke();
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke(ex.Message);
        }
    }

    /// <summary>Export PDF du pointage journalier (état actuel de la grille).</summary>
    public void ExporterSuiviJournalierPdf(string cheminFichier)
    {
        if (EmployeSelectionne == null || PeriodeSelectionnee == null || string.IsNullOrWhiteSpace(cheminFichier))
            return;
        var nom = $"{EmployeSelectionne.Nom} {EmployeSelectionne.Postnom} {EmployeSelectionne.Prenom}".Trim();
        // Source unique: mêmes données/ règles que la fenêtre "Heures du mois"
        // et que l'export global (lecture base + recalcul LT si nécessaire).
        var lignes = SuiviJournalierPdfDataService.ObtenirLignesPourEmploye(
            _db,
            EmployeSelectionne.Id,
            PeriodeSelectionnee.Mois,
            PeriodeSelectionnee.Annee);
        var service = new ExportPdfService();
        service.ExporterSuiviJournalierPdf(
            EmployeSelectionne.Matricule,
            nom,
            EmployeSelectionne.Departement?.NomDepartement,
            PeriodeSelectionnee.Mois,
            PeriodeSelectionnee.Annee,
            lignes,
            cheminFichier);
    }

    /// <summary>
    /// Export PDF des personnels qui ont pointé aujourd'hui (données en base).
    /// </summary>
    public void ExporterPointesAujourdhuiPdf(string cheminFichier)
    {
        if (string.IsNullOrWhiteSpace(cheminFichier))
            return;

        var aujourdHui = DateTime.Today;
        var reglesLt = LtServicesReglesProvider.ChargerDepuisDb(_db);

        var suivisDuJour = _db.SuivisJournaliers
            .AsNoTracking()
            .Where(s => s.Date.Date == aujourdHui &&
                        !string.IsNullOrEmpty(s.PointagesJson) &&
                        s.PointagesJson != "[]")
            .ToList();

        if (suivisDuJour.Count == 0)
        {
            OnErreur?.Invoke("Aucun pointage trouvé aujourd'hui.");
            return;
        }

        var idsEmployes = suivisDuJour.Select(s => s.EmployeId).Distinct().ToList();
        var employes = _db.Employes
            .AsNoTracking()
            .Include(e => e.Departement)
            .Where(e => idsEmployes.Contains(e.Id))
            .ToDictionary(e => e.Id);

        var lignesPdf = new List<PresencePdfLigne>();
        foreach (var suivi in suivisDuJour.OrderBy(s => s.EmployeId))
        {
            if (!employes.TryGetValue(suivi.EmployeId, out var e))
                continue;

            var pointages = PointagesJournalierSerializer.Deserialiser(suivi.PointagesJson, aujourdHui)
                .Select(NormaliserHorodatageLocal)
                .OrderBy(x => x)
                .ToList();
            if (pointages.Count == 0)
                continue;

            var entreeLabel = pointages[0].TimeOfDay <= reglesLt.HeureLimiteTolerance ? "Entrée" : "Entrée (retard)";
            var entree = $"{entreeLabel} {pointages[0]:HH:mm}";
            var debutPause = pointages.Count >= 2 ? pointages[1].ToString("HH:mm") : "—";
            var finPause = pointages.Count >= 3 ? pointages[2].ToString("HH:mm") : "—";
            var sortie = pointages.Count >= 4 ? pointages[3].ToString("HH:mm") : "—";
            var autres = pointages.Count > 4
                ? string.Join(", ", pointages.Skip(4).Select(p => p.ToString("HH:mm")))
                : "—";
            var nom = $"{e.Nom} {e.Postnom} {e.Prenom}".Trim();
            lignesPdf.Add(new PresencePdfLigne(
                aujourdHui.ToString("dd/MM/yyyy"),
                e.Matricule ?? "—",
                nom,
                e.Departement?.NomDepartement ?? "—",
                entree,
                debutPause,
                finPause,
                sortie,
                autres,
                "Reconnu Melody"));
        }

        if (lignesPdf.Count == 0)
        {
            OnErreur?.Invoke("Aucun employé pointé aujourd'hui n'a pu être exporté.");
            return;
        }

        var service = new ExportPdfService();
        service.ExporterPointesAujourdhuiSynthesePdf(
            lignesPdf
                .OrderBy(x => x.NomComplet, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            aujourdHui.Month,
            aujourdHui.Year,
            cheminFichier);
    }

    /// <summary>
    /// Export PDF : page récapitulative + une page détail par employé pour la période sélectionnée (données lues en base, même logique que la grille).
    /// </summary>
    public void ExporterSuiviJournalierPdfTousEmployes(string cheminFichier)
    {
        if (PeriodeSelectionnee == null || string.IsNullOrWhiteSpace(cheminFichier))
            return;
        var mois = PeriodeSelectionnee.Mois;
        var annee = PeriodeSelectionnee.Annee;
        var employes = _db.Employes
            .AsNoTracking()
            .Include(e => e.Departement)
            .OrderBy(e => e.Matricule)
            .ToList();
        var blocs = new List<SuiviJournalierPdfEmployeBloc>();
        foreach (var e in employes)
        {
            var lignes = SuiviJournalierPdfDataService.ObtenirLignesPourEmploye(_db, e.Id, mois, annee);
            var nom = $"{e.Nom} {e.Postnom} {e.Prenom}".Trim();
            blocs.Add(new SuiviJournalierPdfEmployeBloc(e.Matricule, nom, e.Departement?.NomDepartement, lignes));
        }

        var service = new ExportPdfService();
        service.ExporterSuiviJournalierPdfTousEmployes(blocs, mois, annee, cheminFichier);
    }

    /// <summary>Annule les ajustements manuels d’heures pour les jours où des horodatages sont disponibles.</summary>
    private void RetablirCalculAutomatique()
    {
        var reglesLt = LtServicesReglesProvider.ChargerDepuisDb(_db);
        foreach (var ligne in Lignes)
        {
            if (ligne.TypeJour != SuiviJournalier.TypeNormal)
                continue;
            if (string.IsNullOrEmpty(ligne.PointagesJson))
                continue;
            var h = PointagesJournalierSerializer.CalculerHeuresLt(ligne.PointagesJson, ligne.Date, reglesLt);
            ligne.AppliquerHeuresAutomatiques(h);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
