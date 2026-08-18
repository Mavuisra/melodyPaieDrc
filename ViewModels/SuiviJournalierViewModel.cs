using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Helpers;
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
    public int? EmployeId { get; set; }
    public DateTime? DateJour { get; set; }
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
    public string AbsenceLibelle { get; set; } = "—";
    public bool AucuneDonnee { get; set; }
    public bool EstRetard { get; set; }
    public string IndicateurRetard { get; set; } = "À l'heure";
    public int MinutesRetard { get; set; }
    public string DureeRetardLibelle { get; set; } = "—";
    public decimal TauxHoraireUsd { get; set; }
    public decimal TauxHoraireCdf { get; set; }
    public string DeviseContrat { get; set; } = "USD";
    public decimal CoutRetardUsd { get; set; }
    public decimal CoutRetardCdf { get; set; }
    public string HeureLimiteLibelle { get; set; } = "—";

    public string TauxHoraireLibelle =>
        DeviseContrat == "CDF"
            ? (TauxHoraireCdf > 0m ? $"{TauxHoraireCdf:N2} CDF/h" : "—")
            : (TauxHoraireUsd > 0m ? $"{TauxHoraireUsd:N2} USD/h" : "—");

    public string CoutRetardLibelle =>
        !EstRetard || MinutesRetard <= 0
            ? "—"
            : DeviseContrat == "CDF"
                ? $"{CoutRetardCdf:N2} CDF"
                : $"{CoutRetardUsd:N2} USD";

    public string Initiales
    {
        get
        {
            var source = string.IsNullOrWhiteSpace(NomComplet) ? Matricule : NomComplet;
            var parts = (source ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1)
                return parts[0].Length >= 2 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
        }
    }
}

public class PresenceHoraireBarre
{
    public string HeureLabel { get; set; } = "";
    public int NombrePointages { get; set; }
    public double Hauteur { get; set; }
}

public class SuiviJournalierViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private readonly List<Employe> _sourceEmployes = new();
    private Employe? _employeSelectionne;
    private PeriodePaie? _periodeSelectionnee;
    private string _rechercheEmployeText = "";
    private bool _masquerSuggestionsEmploye;
    private bool _applicationSuggestionEmploye;
    private DispatcherTimer? _presenceTimer;
    private string _presenceStatut = "Surveillance automatique au repos.";
    private bool _surveillanceAutomatiqueActive;
    /// <summary>1 pendant une synchro terminal — évite les chevauchements sans bloquer le thread UI entre deux awaits.</summary>
    private int _presenceCycleBusy;
    /// <summary>Intervalle entre deux cycles de synchro (léger pour le PC ; une seule lecture terminal par cycle via la file globale).</summary>
    private static readonly TimeSpan IntervalSurveillancePresence = TimeSpan.FromSeconds(3);

    public static int IntervalleSurveillanceSecondes => (int)IntervalSurveillancePresence.TotalSeconds;

    public int IntervalleLiveSecondes => IntervalleSurveillanceSecondes;

    public bool SonPointageActif
    {
        get => PointageLiveNotificationService.SonActif;
        set
        {
            if (PointageLiveNotificationService.SonActif == value) return;
            PointageLiveNotificationService.SonActif = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SonPointageLibelle));
        }
    }

    public string SonPointageLibelle => SonPointageActif ? "Son activé" : "Son coupé";

    public int CompteurPointagesNonLus => PointageLiveNotificationService.NonLus;

    public bool AfficherBadgePointagesNonLus => PointageLiveNotificationService.AfficherBadge;

    public string BadgePointagesNonLusLibelle => PointageLiveNotificationService.BadgeLibelle;
    private readonly Dictionary<string, List<DateTime>> _historiquePointageParUserJour = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Dernière lecture terminal (jour courant) — inclut les PIN non reconnus en base.</summary>
    private List<(string CodePin, DateTime HorodatageLocal)> _logsTerminalAujourdhui = new();
    private string _zkConnexionResume = "";
    private DateTime? _zkDerniereSyncUtc;
    private DateTime _derniereMajUiSyncUtc;
    private string _presenceResumeDureesAujourdhui = "Aujourd’hui — aucun pointage pour le moment.";
    private string _presenceEnteteColonnePause = "Début pause";
    private bool _presenceAfficherColonnesPause = true;
    private bool _presenceAfficherColonneFinPause = true;
    private bool _terminalHorsLigne;
    private string _diagnosticTechniqueTerminal = "";
    private bool _diagnosticTechniqueVisible;
    private decimal _heuresMoyennesAujourdhui;
    private int _retardsAujourdhui;
    private int _vuePointageIndex;
    private bool _isPresenceFocusMode;
    private PresenceEmployeSyntheseLigne? _presenceLigneSelectionnee;
    private decimal _coutTotalRetardsUsd;
    private decimal _coutTotalRetardsCdf;
    private string _heureLimiteToleranceLibelle = "—";
    private readonly Dictionary<int, (decimal TauxUsd, decimal TauxCdf, string Devise)> _remunerationParEmploye = new();

    public SuiviJournalierViewModel(PaieDbContext db)
    {
        _db = db;
        Employes = new ObservableCollection<Employe>();
        SuggestionsEmployes = new ObservableCollection<Employe>();
        PeriodesPaie = new ObservableCollection<PeriodePaie>();
        Lignes = new ObservableCollection<SuiviJournalierLigne>();
        static bool PeutMod() => DroitsUi.PeutModifier;
        EnregistrerCommand = new RelayCommand(_ => Enregistrer(),
            _ => PeutMod() && EmployeSelectionne != null && PeriodeSelectionnee != null);
        ChargerEmployesCommand = new RelayCommand(_ => ChargerEmployes());
        ChargerPeriodesCommand = new RelayCommand(_ => ChargerPeriodes());
        RechercherEmployeCommand = new RelayCommand(_ => RechercherEmployes());
        SelectionnerSuggestionEmployeCommand = new RelayCommand(p => AppliquerSuggestionEmploye(p as Employe));
        ImporterUtilisateursTerminalCommand = new RelayCommand(_ => ImporterUtilisateursTerminal(), _ => PeutMod());
        ChargerLignesCommand = new RelayCommand(_ => ChargerLignes(), _ => EmployeSelectionne != null && PeriodeSelectionnee != null);
        RetablirCalculAutomatiqueCommand = new RelayCommand(_ => RetablirCalculAutomatique(),
            _ => PeutMod() && EmployeSelectionne != null && PeriodeSelectionnee != null);
        BasculerSonPointageCommand = new RelayCommand(_ => SonPointageActif = !SonPointageActif);
        ReinitialiserBadgePointageCommand = new RelayCommand(_ =>
        {
            PointageLiveNotificationService.ReinitialiserBadge();
            NotifierBadgePointage();
        });
        RafraichirPresenceCommand = new RelayCommand(async _ => await TraiterCyclePresenceAsync().ConfigureAwait(true));
        BasculerDiagnosticCommand = new RelayCommand(_ => DiagnosticTechniqueVisible = !DiagnosticTechniqueVisible);
        EffacerEmployeSelectionneCommand = new RelayCommand(_ => EffacerRechercheEmploye());
        ForcerPeriodeMoisCourantCommand = new RelayCommand(_ => ForcerPeriodeMoisCourant());
        ActualiserListesCommand = new RelayCommand(_ =>
        {
            ChargerEmployes();
            ChargerPeriodes();
            RafraichirAffichageTerminalDepuisBase();
        });
        SelectionnerVuePointageCommand = new RelayCommand(p =>
        {
            if (p is int i)
                VuePointageIndex = i;
            else if (p is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx))
                VuePointageIndex = idx;
        });
        ActiverPresenceFocusCommand = new RelayCommand(
            _ => ActiverPresenceFocus(),
            _ => !IsPresenceFocusMode && VuePointageIndex == 0);
        QuitterPresenceFocusCommand = new RelayCommand(
            _ => QuitterPresenceFocus(),
            _ => IsPresenceFocusMode);
        PointageLiveNotificationService.EtatChange += NotifierBadgePointage;
        ZktecoSynchronisationService.SynchroReussie += OnSynchroZkReussie;
        ZkTerminalParametresNotifier.ParametresModifies += OnZkTerminalParametresModifiesDepuisAutreEcran;
        RafraichirAffichageTerminalDepuisBase();
    }

    public ObservableCollection<Employe> Employes { get; }
    public ObservableCollection<Employe> SuggestionsEmployes { get; }
    public ObservableCollection<PeriodePaie> PeriodesPaie { get; }
    public ObservableCollection<SuiviJournalierLigne> Lignes { get; }
    public ObservableCollection<PresencePointageLigne> PresencePointages { get; } = new();
    public ObservableCollection<PresenceEmployeSyntheseLigne> PresenceSyntheseEmployes { get; } = new();
    public ObservableCollection<PresenceEmployeSyntheseLigne> HistoriquePeriodeLignes { get; } = new();
    public ObservableCollection<PresenceEmployeSyntheseLigne> RetardsDuJour { get; } = new();
    public ObservableCollection<PresenceHoraireBarre> PresenceParHeure { get; } = new();

    /// <summary>0 = présence live, 1 = rapport mouvements, 2 = gestion retards.</summary>
    public int VuePointageIndex
    {
        get => _vuePointageIndex;
        set
        {
            if (_vuePointageIndex == value) return;
            _vuePointageIndex = value;
            OnPropertyChanged();
            if (_vuePointageIndex != 0)
                QuitterPresenceFocus();
            if (_vuePointageIndex == 1)
                ChargerHistoriquePeriode();
        }
    }

    public PresenceEmployeSyntheseLigne? PresenceLigneSelectionnee
    {
        get => _presenceLigneSelectionnee;
        set
        {
            if (ReferenceEquals(_presenceLigneSelectionnee, value)) return;
            _presenceLigneSelectionnee = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PresenceLigneSelectionneeLibelle));
        }
    }

    public string PresenceLigneSelectionneeLibelle =>
        _presenceLigneSelectionnee == null
            ? "Aucun agent sélectionné"
            : $"{_presenceLigneSelectionnee.NomComplet} ({_presenceLigneSelectionnee.Matricule})";

    public string HeureLimiteToleranceLibelle => _heureLimiteToleranceLibelle;

    public string CoutTotalRetardsUsdLibelle =>
        _coutTotalRetardsUsd > 0m ? $"{_coutTotalRetardsUsd:N2} USD" : "—";

    public string CoutTotalRetardsCdfLibelle =>
        _coutTotalRetardsCdf > 0m ? $"{_coutTotalRetardsCdf:N2} CDF" : "—";

    /// <summary>Résumé des durées du jour selon les règles LT de l’entreprise.</summary>
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

    /// <summary>En-tête colonne pause (mode 3 : « Pause », mode 4 : « Début pause »).</summary>
    public string PresenceEnteteColonnePause
    {
        get => _presenceEnteteColonnePause;
        private set
        {
            if (_presenceEnteteColonnePause == value) return;
            _presenceEnteteColonnePause = value ?? "Début pause";
            OnPropertyChanged(nameof(PresenceEnteteColonnePause));
        }
    }

    public bool PresenceAfficherColonnesPause
    {
        get => _presenceAfficherColonnesPause;
        private set
        {
            if (_presenceAfficherColonnesPause == value) return;
            _presenceAfficherColonnesPause = value;
            OnPropertyChanged(nameof(PresenceAfficherColonnesPause));
        }
    }

    public bool PresenceAfficherColonneFinPause
    {
        get => _presenceAfficherColonneFinPause;
        private set
        {
            if (_presenceAfficherColonneFinPause == value) return;
            _presenceAfficherColonneFinPause = value;
            OnPropertyChanged(nameof(PresenceAfficherColonneFinPause));
        }
    }

    /// <summary>Texte d’aide sous la synthèse (colonnes selon le mode de pointage).</summary>
    public string PresenceLegendeColonnes =>
        PresenceAfficherColonneFinPause
            ? "Mode 4 pointages : entrée, début/fin de pause, sortie. « Autres » = lectures supplémentaires au-delà des 4."
            : PresenceAfficherColonnesPause
                ? "Mode 3 pointages : entrée, pause, sortie. « Autres » = lectures supplémentaires au-delà des 3."
                : "Mode 2 pointages : seules les colonnes Entrée et Sortie sont utilisées pour le calcul. « Autres » = 3e lecture et suivantes (hors calcul principal).";

    public bool TerminalHorsLigne
    {
        get => _terminalHorsLigne;
        private set
        {
            if (_terminalHorsLigne == value) return;
            _terminalHorsLigne = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TerminalEtatTitre));
            OnPropertyChanged(nameof(TerminalEtatMessageCourt));
            OnPropertyChanged(nameof(TerminalEtatCouleur));
        }
    }

    public string TerminalEtatTitre => TerminalHorsLigne ? "Terminal hors ligne" : "Terminal connecté";

    public string TerminalEtatMessageCourt =>
        TerminalHorsLigne
            ? "Impossible de joindre le terminal de pointage."
            : "Connexion active, synchronisation en temps réel.";

    public string TerminalEtatCouleur => TerminalHorsLigne ? "#E53935" : "#2E7D32";

    public string DiagnosticTechniqueTerminal
    {
        get => _diagnosticTechniqueTerminal;
        private set
        {
            if (_diagnosticTechniqueTerminal == value) return;
            _diagnosticTechniqueTerminal = value ?? "";
            OnPropertyChanged();
        }
    }

    public bool DiagnosticTechniqueVisible
    {
        get => _diagnosticTechniqueVisible;
        set
        {
            if (_diagnosticTechniqueVisible == value) return;
            _diagnosticTechniqueVisible = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BoutonDiagnosticLibelle));
        }
    }

    public string BoutonDiagnosticLibelle => DiagnosticTechniqueVisible ? "Masquer les détails" : "Voir les détails";

    public int NbEmployesPresentsAujourdhui => PresenceSyntheseEmployes.Count;

    public int NbRetardsAujourdhui => _retardsAujourdhui;

    public string HeuresMoyennesAujourdhuiLibelle => _heuresMoyennesAujourdhui <= 0m
        ? "—"
        : $"{_heuresMoyennesAujourdhui:N2} h";

    public string DateDuJourLibelle =>
        DateTime.Today.ToString("dddd d MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("fr-FR"));

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
            (RetablirCalculAutomatiqueCommand as RelayCommand)?.RaiseCanExecuteChanged();
            if (value != null && PeriodeSelectionnee != null)
                ChargerLignes();
            ChargerHistoriquePeriode();
            OnPropertyChanged(nameof(ContexteSaisieLibelle));
            OnPropertyChanged(nameof(AfficherDetailMois));
            OnPropertyChanged(nameof(AfficherInviteSelectionEmploye));
            OnPropertyChanged(nameof(AfficherDetailMoisHorsFocus));
            OnPropertyChanged(nameof(AfficherInviteSelectionEmployeHorsFocus));
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
            (RetablirCalculAutomatiqueCommand as RelayCommand)?.RaiseCanExecuteChanged();
            if (value != null && EmployeSelectionne != null)
                ChargerLignes();
            ChargerHistoriquePeriode();
            OnPropertyChanged(nameof(ContexteSaisieLibelle));
            OnPropertyChanged(nameof(AfficherDetailMois));
            OnPropertyChanged(nameof(AfficherInviteSelectionEmploye));
            OnPropertyChanged(nameof(AfficherDetailMoisHorsFocus));
            OnPropertyChanged(nameof(AfficherInviteSelectionEmployeHorsFocus));
        }
    }

    public bool AfficherDetailMois => EmployeSelectionne != null && PeriodeSelectionnee != null;

    public bool AfficherInviteSelectionEmploye =>
        PeriodeSelectionnee != null && EmployeSelectionne == null && _sourceEmployes.Count > 0;

    public bool AfficherDetailMoisHorsFocus => AfficherDetailMois && !IsPresenceFocusMode;

    public bool AfficherInviteSelectionEmployeHorsFocus =>
        AfficherInviteSelectionEmploye && !IsPresenceFocusMode;

    public bool PresenceListeVide => PresenceSyntheseEmployes.Count == 0;

    public string HistoriquePeriodeResume
    {
        get
        {
            if (PeriodeSelectionnee == null)
                return "Sélectionnez une période (mois / année) pour consulter l’historique.";
            var filtre = EmployeSelectionne == null
                ? "tous les employés"
                : $"{EmployeSelectionne.Nom} {EmployeSelectionne.Prenom}".Trim();
            if (HistoriquePeriodeLignes.Count == 0)
                return $"Aucun pointage pour {PeriodeSelectionnee.Mois:D2}/{PeriodeSelectionnee.Annee} ({filtre}).";
            var vides = HistoriquePeriodeLignes.Count(l => l.AucuneDonnee);
            return vides > 0
                ? $"{HistoriquePeriodeLignes.Count} ligne(s) — {vides} jour(s) sans donnée ({filtre})."
                : $"{HistoriquePeriodeLignes.Count} ligne(s) pour {PeriodeSelectionnee.Mois:D2}/{PeriodeSelectionnee.Annee} ({filtre}).";
        }
    }

    public bool HistoriquePeriodeVide => HistoriquePeriodeLignes.Count == 0;

    public string ContexteSaisieLibelle
    {
        get
        {
            if (EmployeSelectionne == null || PeriodeSelectionnee == null)
                return "";
            var nom = $"{EmployeSelectionne.Nom} {EmployeSelectionne.Postnom} {EmployeSelectionne.Prenom}".Trim();
            return $"Suivi mois — {nom} ({EmployeSelectionne.Matricule}) · {PeriodeSelectionnee.Mois:D2}/{PeriodeSelectionnee.Annee}";
        }
    }

    public string TotalHeuresMoisLibelle =>
        Lignes.Count == 0 ? "—" : $"{Lignes.Sum(l => l.HeuresPrestees):N2} h sur {Lignes.Count} jour(s)";

    public ICommand EnregistrerCommand { get; }
    public ICommand ChargerEmployesCommand { get; }
    public ICommand ChargerPeriodesCommand { get; }
    public ICommand RechercherEmployeCommand { get; }
    public ICommand SelectionnerSuggestionEmployeCommand { get; }
    public ICommand ImporterUtilisateursTerminalCommand { get; }
    public ICommand ChargerLignesCommand { get; }

    /// <summary>Réapplique le calcul LTservices sur toutes les lignes qui ont des horodatages en base.</summary>
    public ICommand RetablirCalculAutomatiqueCommand { get; }
    public ICommand BasculerSonPointageCommand { get; }
    public ICommand ReinitialiserBadgePointageCommand { get; }
    public ICommand RafraichirPresenceCommand { get; }
    public ICommand BasculerDiagnosticCommand { get; }
    public ICommand EffacerEmployeSelectionneCommand { get; }
    public ICommand ForcerPeriodeMoisCourantCommand { get; }
    public ICommand ActualiserListesCommand { get; }
    public ICommand SelectionnerVuePointageCommand { get; }
    public ICommand ActiverPresenceFocusCommand { get; }
    public ICommand QuitterPresenceFocusCommand { get; }

    /// <summary>Agrandit « Présence du jour » dans la fenêtre actuelle, sans plein écran Windows.</summary>
    public bool IsPresenceFocusMode
    {
        get => _isPresenceFocusMode;
        private set
        {
            if (_isPresenceFocusMode == value) return;
            _isPresenceFocusMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AfficherDetailMoisHorsFocus));
            OnPropertyChanged(nameof(AfficherInviteSelectionEmployeHorsFocus));
            PresenceFocusModeChanged?.Invoke(value);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public event Action<bool>? PresenceFocusModeChanged;

    public void ActiverPresenceFocus()
    {
        if (VuePointageIndex != 0)
            return;
        IsPresenceFocusMode = true;
    }

    public void QuitterPresenceFocus() => IsPresenceFocusMode = false;

    private void NotifierBadgePointage()
    {
        OnPropertyChanged(nameof(CompteurPointagesNonLus));
        OnPropertyChanged(nameof(AfficherBadgePointagesNonLus));
        OnPropertyChanged(nameof(BadgePointagesNonLusLibelle));
        OnPropertyChanged(nameof(NbEmployesPresentsAujourdhui));
        OnPropertyChanged(nameof(NbRetardsAujourdhui));
        OnPropertyChanged(nameof(HeuresMoyennesAujourdhuiLibelle));
    }

    /// <summary>Rafraîchit les commandes après connexion / déconnexion.</summary>
    public void NotifierDroitsModification()
    {
        (EnregistrerCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ImporterUtilisateursTerminalCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RetablirCalculAutomatiqueCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ChargerLignesCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    /// <summary>Appelé après enregistrement réussi (message utilisateur ou fermeture fenêtre modale).</summary>
    public Action? OnSauvegardeReussie { get; set; }
    public Action<string>? OnErreur { get; set; }
    public Action<string>? OnMessageInformation { get; set; }

    public string RechercheEmployeText
    {
        get => _rechercheEmployeText;
        set
        {
            if (_rechercheEmployeText == value) return;
            _rechercheEmployeText = value ?? "";
            if (!_applicationSuggestionEmploye)
                _masquerSuggestionsEmploye = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AfficherEffacerRechercheEmploye));
            RechercherEmployes();
        }
    }

    public bool AfficherSuggestionsEmployes =>
        !_masquerSuggestionsEmploye
        && !_applicationSuggestionEmploye
        && !string.IsNullOrWhiteSpace(RechercheEmployeText)
        && SuggestionsEmployes.Count > 0;

    public bool AfficherEffacerRechercheEmploye =>
        !string.IsNullOrWhiteSpace(RechercheEmployeText) || EmployeSelectionne != null;

    public string RechercheEmployeResultatLibelle
    {
        get
        {
            if (EmployeSelectionne != null && _masquerSuggestionsEmploye)
                return $"Sélectionné : {EmployeSelectionne.NomComplet} ({EmployeSelectionne.Matricule})";
            if (string.IsNullOrWhiteSpace(RechercheEmployeText))
                return _sourceEmployes.Count == 0
                    ? "Aucun employé enregistré"
                    : "Tapez un nom ou un matricule pour choisir un employé";
            if (SuggestionsEmployes.Count == 0)
                return $"Aucun employé pour « {RechercheEmployeText.Trim()} »";
            return $"{SuggestionsEmployes.Count} suggestion(s)";
        }
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
        {
            RafraichirAffichageTerminalDepuisBase();
            RecalculerSynthesePresenceEmployes();
        }
        else
            app?.Dispatcher.Invoke(() =>
            {
                RafraichirAffichageTerminalDepuisBase();
                RecalculerSynthesePresenceEmployes();
            });
    }

    /// <summary>Recharge grilles et synthèse présence après changement du mode de pointage ou des horaires LT.</summary>
    public void RafraichirApresChangementReglesLt()
    {
        RecalculerSynthesePresenceEmployes();
        RecalculerResumeDureesAujourdhui();
        if (EmployeSelectionne != null && PeriodeSelectionnee != null)
            ChargerLignes();
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
        SuiviJournalier.TypeCongeAnnuel,
        SuiviJournalier.TypeCongeCirconstance,
        SuiviJournalier.TypeMaladie,
        SuiviJournalier.TypePreavis
    };

    /// <summary>Message affiché quand aucune donnée (période ou employé manquant).</summary>
    public string MessageVide
    {
        get
        {
            if (_sourceEmployes.Count == 0 && PeriodesPaie.Count == 0)
                return "Ajoutez d'abord des employés (menu Employés) et des périodes de paie (Paramètres → Périodes de paie).";
            if (_sourceEmployes.Count == 0)
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
        _sourceEmployes.AddRange(
            ContexteEntrepriseService.EmployesEntrepriseCourante(_db)
                .Include(x => x.Departement)
                .OrderBy(x => x.Nom)
                .ThenBy(x => x.Prenom));
        ActualiserRemunerationsEmployes();
        RechercherEmployes();
        OnPropertyChanged(nameof(MessageVide));
    }

    private void ActualiserRemunerationsEmployes()
    {
        _remunerationParEmploye.Clear();
        if (_sourceEmployes.Count == 0)
            return;

        var ids = _sourceEmployes.Select(e => e.Id).ToList();
        var contratsParEmploye = _db.Contrats.AsNoTracking()
            .Where(c => ids.Contains(c.EmployeId))
            .OrderByDescending(c => c.DateDebut)
            .ToList()
            .GroupBy(c => c.EmployeId)
            .ToDictionary(g => g.Key, g => g.First());

        var tauxCdfUsd = ParametresApplicationHelper.GetTauxCdfParUsd(_db);
        var entrepriseId = ContexteEntrepriseService.ObtenirEntrepriseCouranteId(_db);
        var politiquePaie = new PolitiquePaieService(_db).Charger(entrepriseId);

        foreach (var e in _sourceEmployes)
        {
            e.JoursReferencePaie = politiquePaie.JoursReferencePaie;
            e.HeuresParJour = politiquePaie.HeuresParJour;

            if (!contratsParEmploye.TryGetValue(e.Id, out var c))
                continue;

            var devise = (c.DeviseBase ?? "USD").Trim().ToUpperInvariant();
            decimal tauxUsd;
            decimal tauxCdf;
            if (devise == "CDF")
            {
                tauxCdf = SalaireReferenceHelper.SalaireHeure(c.SalaireBase, politiquePaie.JoursReferencePaie, politiquePaie.HeuresParJour);
                tauxUsd = tauxCdfUsd > 0
                    ? decimal.Round(tauxCdf / tauxCdfUsd, 4, MidpointRounding.AwayFromZero)
                    : 0m;
            }
            else
            {
                tauxUsd = SalaireReferenceHelper.SalaireHeure(c.SalaireBase, politiquePaie.JoursReferencePaie, politiquePaie.HeuresParJour);
                tauxCdf = decimal.Round(tauxUsd * tauxCdfUsd, 2, MidpointRounding.AwayFromZero);
                devise = "USD";
            }

            _remunerationParEmploye[e.Id] = (tauxUsd, tauxCdf, devise);
        }
    }

    private (decimal TauxUsd, decimal TauxCdf, string Devise) ResoudreRemunerationEmploye(int? employeId)
    {
        if (employeId is null or <= 0 || !_remunerationParEmploye.TryGetValue(employeId.Value, out var rem))
            return (0m, 0m, "USD");
        return rem;
    }

    private static string FormaterDureeRetard(int minutes) => RetardPaieHelper.FormaterDureeRetard(minutes);

    private PolitiquePaieContext ChargerPolitiqueCourante() =>
        new PolitiquePaieService(_db).Charger(ContexteEntrepriseService.ObtenirEntrepriseCouranteId(_db));

    private void RechercherEmployes()
    {
        var filtre = (RechercheEmployeText ?? "").Trim();
        Employes.Clear();
        foreach (var e in RechercheEmployeHelper.Filtrer(_sourceEmployes, filtre))
            Employes.Add(e);

        SuggestionsEmployes.Clear();
        if (!_masquerSuggestionsEmploye && !string.IsNullOrWhiteSpace(filtre))
        {
            foreach (var e in RechercheEmployeHelper.Suggerer(_sourceEmployes, filtre))
                SuggestionsEmployes.Add(e);
        }

        OnPropertyChanged(nameof(AfficherSuggestionsEmployes));
        OnPropertyChanged(nameof(RechercheEmployeResultatLibelle));
        OnPropertyChanged(nameof(MessageVide));
        ChargerHistoriquePeriode();
    }

    private void AppliquerSuggestionEmploye(Employe? employe)
    {
        if (employe == null) return;

        _applicationSuggestionEmploye = true;
        _masquerSuggestionsEmploye = true;
        try
        {
            EmployeSelectionne = employe;
            _rechercheEmployeText = $"{employe.Matricule} — {employe.NomComplet}".Trim(' ', '—');
            OnPropertyChanged(nameof(RechercheEmployeText));
            OnPropertyChanged(nameof(AfficherEffacerRechercheEmploye));
            SuggestionsEmployes.Clear();
            OnPropertyChanged(nameof(AfficherSuggestionsEmployes));
            OnPropertyChanged(nameof(RechercheEmployeResultatLibelle));
        }
        finally
        {
            _applicationSuggestionEmploye = false;
        }
    }

    private void EffacerRechercheEmploye()
    {
        _masquerSuggestionsEmploye = true;
        EmployeSelectionne = null;
        RechercheEmployeText = "";
        SuggestionsEmployes.Clear();
        OnPropertyChanged(nameof(AfficherSuggestionsEmployes));
        OnPropertyChanged(nameof(RechercheEmployeResultatLibelle));
    }

    /// <summary>À appeler lorsque l’utilisateur affiche l’écran pointage (panneau visible).</summary>
    public void DemarrerSurveillancePresenceAutomatique()
    {
        if (_surveillanceAutomatiqueActive && _presenceTimer != null)
            return;

        PresenceSurveillanceActive = true;
        PresenceStatut = "Surveillance automatique — synchronisation du terminal…";
        _logsTerminalAujourdhui.Clear();
        RecalculerSynthesePresenceEmployes();
        RecalculerResumeDureesAujourdhui();
        RecalculerGraphiquePresenceParHeure();
        NotifierBadgePointage();

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
                TerminalHorsLigne = true;
                DiagnosticTechniqueTerminal = errPre ?? "Configuration terminal incomplète.";
                PresenceStatut = "Terminal indisponible — vérifiez la connexion.";
                return;
            }

            PresenceStatut = "Synchronisation du terminal…";
            var (ok, logs, err, nbNouveaux) = await ZktecoSynchronisationService.TrySynchroniserAvecLogsAsync().ConfigureAwait(true);
            if (!ok)
            {
                TerminalHorsLigne = true;
                DiagnosticTechniqueTerminal = string.IsNullOrWhiteSpace(err) ? "Échec de la synchronisation." : err!;
                PresenceStatut = "Terminal hors ligne — réessayez.";
                return;
            }

            TerminalHorsLigne = false;
            DiagnosticTechniqueTerminal = "";
            RafraichirAffichageTerminalDepuisBase();
            if (PeriodeSelectionnee != null)
                ChargerLignes();

            MettreAJourLogsTerminalAujourdhui(logs);
            RecalculerSynthesePresenceEmployes();
            RecalculerResumeDureesAujourdhui();
            ChargerHistoriquePeriode();
            NotifierBadgePointage();

            var nbPersonnes = PresenceSyntheseEmployes.Count;
            PresenceStatut = nbPersonnes > 0
                ? (nbNouveaux > 0
                    ? $"{nbPersonnes} personne(s) pointée(s) aujourd'hui — {nbNouveaux} nouveau(x) pointage(s)"
                    : $"{nbPersonnes} personne(s) pointée(s) aujourd'hui")
                : "Surveillance active — en attente de pointage";
            RecalculerGraphiquePresenceParHeure();
        }
        finally
        {
            Interlocked.Exchange(ref _presenceCycleBusy, 0);
            NotifierPresenceSynchronisationEnCours();
        }
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

        var aujourdhui = DateTime.Today;
        _logsTerminalAujourdhui.RemoveAll(l => l.HorodatageLocal.Date != aujourdhui);
    }

    private void MettreAJourLogsTerminalAujourdhui(IReadOnlyList<(string CodePin, DateTime Horodatage)>? logs)
    {
        if (logs == null || logs.Count == 0)
            return;

        var aujourdhui = DateTime.Today;
        _logsTerminalAujourdhui = logs
            .Select(l => (l.CodePin.Trim(), NormaliserHorodatageLocal(l.Horodatage)))
            .Where(l => l.Item2.Date == aujourdhui)
            .ToList();
        RecalculerGraphiquePresenceParHeure();
    }

    private sealed class PresencePersonneJour
    {
        public PresencePersonneJour(Employe? employe, string codeTerminal)
        {
            Employe = employe;
            CodeTerminal = codeTerminal;
        }

        public Employe? Employe { get; }
        public string CodeTerminal { get; }
        public List<DateTime> Pointages { get; } = new();
    }

    /// <summary>Toutes les personnes ayant pointé aujourd'hui (base + terminal, une ligne par employé ou PIN).</summary>
    private Dictionary<string, PresencePersonneJour> CollecterPersonnesPointeesAujourdhui()
    {
        var aujourdhui = DateTime.Today;
        var jourFin = aujourdhui.AddDays(1);
        var mapEmployes = ConstruireMapEmployesPourPresence();
        var parCle = new Dictionary<string, PresencePersonneJour>(StringComparer.OrdinalIgnoreCase);

        void Ajouter(string codePin, DateTime horodatageLocal, Employe? employeConnu = null)
        {
            if (horodatageLocal.Date != aujourdhui)
                return;

            var emp = employeConnu ?? ResoudreEmployePourPresence(mapEmployes, codePin);
            var cle = emp != null ? "E:" + emp.Id : "Z:" + NormaliserCleLocal(codePin);
            if (!parCle.TryGetValue(cle, out var bloc))
            {
                bloc = new PresencePersonneJour(emp, codePin.Trim());
                parCle[cle] = bloc;
            }

            bloc.Pointages.Add(horodatageLocal);
        }

        var suivis = _db.SuivisJournaliers
            .AsNoTracking()
            .Where(s => s.Date >= aujourdhui && s.Date < jourFin &&
                        s.PointagesJson != null && s.PointagesJson != "" && s.PointagesJson != "[]")
            .ToList();

        if (suivis.Count > 0)
        {
            var ids = suivis.Select(s => s.EmployeId).Distinct().ToList();
            var employes = _db.Employes
                .AsNoTracking()
                .Include(e => e.Departement)
                .Where(e => ids.Contains(e.Id))
                .ToDictionary(e => e.Id);

            foreach (var suivi in suivis)
            {
                if (!employes.TryGetValue(suivi.EmployeId, out var emp))
                    continue;
                var codePin = !string.IsNullOrWhiteSpace(emp.ZkUserId) ? emp.ZkUserId! : emp.Matricule ?? emp.Id.ToString(CultureInfo.InvariantCulture);
                foreach (var dt in PointagesJournalierSerializer.Deserialiser(suivi.PointagesJson, aujourdhui))
                    Ajouter(codePin, NormaliserHorodatageLocal(dt), emp);
            }
        }

        foreach (var (pin, dt) in _logsTerminalAujourdhui)
            Ajouter(pin, dt);

        foreach (var bloc in parCle.Values)
        {
            bloc.Pointages.Sort();
            for (var i = bloc.Pointages.Count - 1; i > 0; i--)
            {
                if (bloc.Pointages[i] == bloc.Pointages[i - 1])
                    bloc.Pointages.RemoveAt(i);
            }
        }

        return parCle;
    }

    private static Employe? ResoudreEmployePourPresence(Dictionary<string, Employe> map, string codePin)
    {
        var cle = NormaliserCleLocal(codePin);
        if (!string.IsNullOrWhiteSpace(cle) && map.TryGetValue(cle, out var emp))
            return emp;
        var digits = NormaliserDigitsLocal(codePin);
        if (!string.IsNullOrWhiteSpace(digits) && map.TryGetValue(digits, out emp))
            return emp;
        return null;
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

        RecalculerSynthesePresenceEmployes();
        RecalculerResumeDureesAujourdhui();
    }

    private void MettreAJourEntetesPresenceColonnes(LtServicesRegles regles)
    {
        PresenceAfficherColonnesPause = !regles.UtiliseDeuxPointages;
        PresenceAfficherColonneFinPause = regles.UtiliseQuatrePointages;
        PresenceEnteteColonnePause = regles.UtiliseTroisPointages ? "Pause" : "Début pause";
        OnPropertyChanged(nameof(PresenceLegendeColonnes));
    }

    private void RecalculerSynthesePresenceEmployes()
    {
        var reglesLt = LtServicesReglesProvider.ChargerDepuisDb(_db);
        var politique = ChargerPolitiqueCourante();
        MettreAJourEntetesPresenceColonnes(reglesLt);
        _heureLimiteToleranceLibelle = RetardPaieHelper.LibelleHeureLimite(reglesLt);
        OnPropertyChanged(nameof(HeureLimiteToleranceLibelle));

        PresenceSyntheseEmployes.Clear();
        RetardsDuJour.Clear();
        var parPersonne = CollecterPersonnesPointeesAujourdhui();
        if (parPersonne.Count == 0)
        {
            _retardsAujourdhui = 0;
            _coutTotalRetardsUsd = _coutTotalRetardsCdf = 0m;
            NotifierTotauxRetards();
            OnPropertyChanged(nameof(NbEmployesPresentsAujourdhui));
            OnPropertyChanged(nameof(NbRetardsAujourdhui));
            OnPropertyChanged(nameof(PresenceListeVide));
            return;
        }

        var aujourdhui = DateTime.Today;
        var heuresJour = politique.HeuresParJour > 0 ? politique.HeuresParJour : SalaireReferenceHelper.HeuresDefaut;
        static string HeureMin(DateTime dt) => dt.ToString("HH:mm");

        foreach (var bloc in parPersonne.Values
                     .OrderBy(b => b.Employe != null ? 0 : 1)
                     .ThenBy(b => b.Employe != null ? $"{b.Employe.Nom} {b.Employe.Prenom}" : b.CodeTerminal, StringComparer.CurrentCultureIgnoreCase))
        {
            var pointages = bloc.Pointages;
            if (pointages.Count == 0)
                continue;

            var emp = bloc.Employe;
            var reglesEmp = RetardPaieHelper.ReglesPourEmploye(reglesLt, emp);
            var decoupe = PointagesMomentsHelper.Decouper(pointages, aujourdhui, reglesEmp);
            var estRetard = RetardPaieHelper.EstRetard(decoupe.Entree, reglesEmp.HeureLimiteTolerance);
            var minutesRetard = decoupe.Entree.HasValue
                ? RetardPaieHelper.CalculerMinutesRetard(decoupe.Entree.Value, reglesEmp.HeureLimiteTolerance)
                : 0;
            var autres = decoupe.PointagesSupplementaires.Count > 0
                ? string.Join(", ", decoupe.PointagesSupplementaires.Select(HeureMin))
                : "—";

            var reconnu = emp != null;
            var rem = ResoudreRemunerationEmploye(emp?.Id);
            var salaireJourUsd = rem.TauxUsd * heuresJour;
            var salaireJourCdf = rem.TauxCdf * heuresJour;
            var coutUsd = politique.RetardSanctionActive
                ? RetardPaieHelper.CalculerSanctionJour(politique, minutesRetard, salaireJourUsd, rem.TauxUsd)
                : 0m;
            var coutCdf = politique.RetardSanctionActive
                ? RetardPaieHelper.CalculerSanctionJour(politique, minutesRetard, salaireJourCdf, rem.TauxCdf)
                : 0m;

            var ligne = new PresenceEmployeSyntheseLigne
            {
                EmployeId = emp?.Id,
                Jour = aujourdhui.ToString("dd/MM/yyyy"),
                Matricule = reconnu ? (string.IsNullOrWhiteSpace(emp!.Matricule) ? "—" : emp.Matricule) : bloc.CodeTerminal,
                NomComplet = reconnu
                    ? $"{emp!.Nom} {emp.Postnom} {emp.Prenom}".Trim()
                    : $"Non attribué (ID terminal {bloc.CodeTerminal})",
                Departement = reconnu ? (emp!.Departement?.NomDepartement ?? "—") : "—",
                Entree = decoupe.Entree.HasValue ? HeureMin(decoupe.Entree.Value) : "—",
                DebutPause = decoupe.DebutPause.HasValue ? HeureMin(decoupe.DebutPause.Value) : "—",
                FinPause = decoupe.FinPause.HasValue ? HeureMin(decoupe.FinPause.Value) : "—",
                Sortie = decoupe.Sortie.HasValue ? HeureMin(decoupe.Sortie.Value) : "—",
                Autres = autres,
                Statut = reconnu ? "Reconnu Melody" : "Non reconnu Melody",
                EstRetard = estRetard,
                IndicateurRetard = estRetard ? "En retard" : "À l'heure",
                MinutesRetard = minutesRetard,
                DureeRetardLibelle = FormaterDureeRetard(minutesRetard),
                TauxHoraireUsd = rem.TauxUsd,
                TauxHoraireCdf = rem.TauxCdf,
                DeviseContrat = rem.Devise,
                CoutRetardUsd = coutUsd,
                CoutRetardCdf = coutCdf,
                HeureLimiteLibelle = RetardPaieHelper.LibelleHeureLimite(reglesEmp)
            };
            PresenceSyntheseEmployes.Add(ligne);
            if (estRetard)
                RetardsDuJour.Add(ligne);
        }

        _retardsAujourdhui = RetardsDuJour.Count;
        _coutTotalRetardsUsd = RetardsDuJour.Sum(x => x.CoutRetardUsd);
        _coutTotalRetardsCdf = RetardsDuJour.Sum(x => x.CoutRetardCdf);
        NotifierTotauxRetards();
        OnPropertyChanged(nameof(NbEmployesPresentsAujourdhui));
        OnPropertyChanged(nameof(NbRetardsAujourdhui));
        OnPropertyChanged(nameof(PresenceListeVide));
    }

    private void NotifierTotauxRetards()
    {
        OnPropertyChanged(nameof(CoutTotalRetardsUsdLibelle));
        OnPropertyChanged(nameof(CoutTotalRetardsCdfLibelle));
    }

    private void RecalculerResumeDureesAujourdhui()
    {
        var aujourdhui = DateTime.Today.Date;
        var parPersonne = CollecterPersonnesPointeesAujourdhui();
        if (parPersonne.Count == 0)
        {
            PresenceResumeDureesAujourdhui = "Aujourd’hui — aucun pointage pour le moment.";
            _heuresMoyennesAujourdhui = 0m;
            OnPropertyChanged(nameof(HeuresMoyennesAujourdhuiLibelle));
            return;
        }

        var reglesLt = LtServicesReglesProvider.ChargerDepuisDb(_db);
        var parties = new List<string>();
        decimal totalHeures = 0m;
        var nbAvecHeures = 0;
        foreach (var bloc in parPersonne.Values
                     .OrderBy(b => b.Employe != null ? $"{b.Employe!.Nom} {b.Employe.Prenom}" : b.CodeTerminal, StringComparer.CurrentCultureIgnoreCase))
        {
            var horaires = bloc.Pointages;
            if (horaires.Count == 0)
                continue;

            var libelle = bloc.Employe != null
                ? $"{bloc.Employe.Nom} {bloc.Employe.Prenom}".Trim()
                : $"ID {bloc.CodeTerminal}";
            var refId = bloc.Employe?.Matricule ?? bloc.CodeTerminal;
            if (horaires.Count == 1)
            {
                parties.Add($"{libelle} ({refId}) — 1 pointage");
                continue;
            }

            var heures = LtServicesPointageCalcul.CalculerHeuresPrestees(horaires, aujourdhui, reglesLt);
            if (heures > 0m)
            {
                totalHeures += heures;
                nbAvecHeures++;
            }
            parties.Add($"{libelle} ({refId}) — {heures.ToString("N2", CultureInfo.CurrentCulture)} h");
        }

        PresenceResumeDureesAujourdhui =
            "Durées du jour (calcul selon règles de service) — "
            + string.Join(" · ", parties);
        _heuresMoyennesAujourdhui = nbAvecHeures == 0 ? 0m : totalHeures / nbAvecHeures;
        OnPropertyChanged(nameof(HeuresMoyennesAujourdhuiLibelle));
    }

    private void RecalculerGraphiquePresenceParHeure()
    {
        var aujourdHui = DateTime.Today;
        var groupes = _logsTerminalAujourdhui
            .Where(x => x.HorodatageLocal.Date == aujourdHui)
            .GroupBy(x => x.HorodatageLocal.Hour)
            .ToDictionary(g => g.Key, g => g.Count());

        const int debut = 6;
        const int fin = 20;
        var max = groupes.Count == 0 ? 1 : groupes.Values.Max();
        PresenceParHeure.Clear();
        for (var h = debut; h <= fin; h++)
        {
            groupes.TryGetValue(h, out var count);
            var hauteur = count == 0 ? 6 : 8 + (44d * count / max);
            PresenceParHeure.Add(new PresenceHoraireBarre
            {
                HeureLabel = $"{h:D2}h",
                NombrePointages = count,
                Hauteur = hauteur
            });
        }
        OnPropertyChanged(nameof(PresenceParHeure));
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

        if (logsJour.Count > 0 && local - logsJour[^1] < PointagesNettoyageHelper.IntervalleAntiDoublon)
            return "Lecture en double (ignorée)";

        string moment;
        var entreeLabel = t <= reglesLt.HeureLimiteTolerance ? "Entrée" : "Entrée (retard)";
        if (reglesLt.UtiliseDeuxPointages)
        {
            moment = logsJour.Count switch
            {
                0 => entreeLabel,
                1 => "Sortie",
                _ => "Pointage supplémentaire"
            };
        }
        else if (reglesLt.UtiliseTroisPointages)
        {
            moment = logsJour.Count switch
            {
                0 => entreeLabel,
                1 => "Pause",
                2 => "Sortie",
                _ => "Pointage supplémentaire"
            };
        }
        else
        {
            moment = logsJour.Count switch
            {
                0 => entreeLabel,
                1 => "Début pause",
                2 => "Fin pause",
                3 => "Sortie",
                _ => "Pointage supplémentaire"
            };
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

    /// <summary>Période du mois calendaire courant (sans présélection d'employé — choix via le tableau live ou la liste).</summary>
    public void SelectionnerPeriodeMoisCourant()
    {
        if (PeriodesPaie.Count == 0)
        {
            PeriodeSelectionnee = null;
            return;
        }

        if (PeriodeSelectionnee != null)
            return;

        ForcerPeriodeMoisCourant();
    }

    public void SelectionnerPremiereSuggestionEmploye()
    {
        var premier = SuggestionsEmployes.FirstOrDefault();
        if (premier != null)
            AppliquerSuggestionEmploye(premier);
    }

    /// <summary>Force la période sur le mois calendaire en cours (bouton d'action).</summary>
    public void ForcerPeriodeMoisCourant()
    {
        if (PeriodesPaie.Count == 0)
        {
            PeriodeSelectionnee = null;
            return;
        }

        var now = DateTime.Today;
        PeriodeSelectionnee = PeriodesPaie.FirstOrDefault(p => p.Mois == now.Month && p.Annee == now.Year)
            ?? PeriodesPaie.OrderByDescending(p => p.Annee).ThenByDescending(p => p.Mois).First();
    }

    /// <summary>Sélectionne l'employé depuis une ligne du tableau présence (suivi en direct).</summary>
    public void SelectionnerEmployeParId(int employeId)
    {
        var emp = Employes.FirstOrDefault(e => e.Id == employeId)
                  ?? _sourceEmployes.FirstOrDefault(e => e.Id == employeId);
        if (emp != null)
            AppliquerSuggestionEmploye(emp);
    }

    public void SelectionnerEmployeParMatricule(string? matricule)
    {
        if (string.IsNullOrWhiteSpace(matricule) || matricule == "—")
            return;

        var cle = matricule.Trim();
        var emp = Employes.FirstOrDefault(e =>
                string.Equals(e.Matricule?.Trim(), cle, StringComparison.OrdinalIgnoreCase))
            ?? _sourceEmployes.FirstOrDefault(e =>
                string.Equals(e.Matricule?.Trim(), cle, StringComparison.OrdinalIgnoreCase));

        if (emp != null)
            AppliquerSuggestionEmploye(emp);
    }

    public void ChargerHistoriquePeriode()
    {
        HistoriquePeriodeLignes.Clear();
        if (PeriodeSelectionnee == null)
        {
            OnPropertyChanged(nameof(HistoriquePeriodeResume));
            OnPropertyChanged(nameof(HistoriquePeriodeVide));
            return;
        }

        try
        {
            var lignes = HistoriquePointagePeriodeService.Charger(
                _db,
                PeriodeSelectionnee,
                EmployeSelectionne?.Id,
                EmployeSelectionne != null
                    ? null
                    : (string.IsNullOrWhiteSpace(RechercheEmployeText) ? null : RechercheEmployeText));
            foreach (var l in lignes)
                HistoriquePeriodeLignes.Add(l);
        }
        catch
        {
            // L'historique ne doit pas interrompre la surveillance live.
        }

        OnPropertyChanged(nameof(HistoriquePeriodeResume));
        OnPropertyChanged(nameof(HistoriquePeriodeVide));
    }

    public void AfficherHistoriquePourEmploye(int employeId)
    {
        SelectionnerEmployeParId(employeId);
        VuePointageIndex = 1;
        ChargerHistoriquePeriode();
    }

    public void ChargerLignes()
    {
        foreach (var ligne in Lignes)
            ligne.PropertyChanged -= LigneSuiviPropertyChanged;
        Lignes.Clear();
        OnPropertyChanged(nameof(TotalHeuresMoisLibelle));
        if (EmployeSelectionne == null || PeriodeSelectionnee == null) return;

        try
        {
            var reglesLt = LtServicesReglesProvider.ChargerDepuisDb(_db);
            var (politique, dateDebut, dateFin) = PeriodePaieHelper.ResoudrePeriode(_db, PeriodeSelectionnee);
            var employeId = EmployeSelectionne.Id;

            var existantsList = _db.SuivisJournaliers
                .Where(s => s.EmployeId == employeId && s.Date >= dateDebut && s.Date <= dateFin)
                .ToList();
            var existants = existantsList.ToDictionary(s => s.Date.Date);

            var calendrierCtx = SuiviJournalierCalculPaieHelper.ChargerCalendrierPaie(_db, dateDebut, dateFin);
            var semaineSixJours = calendrierCtx.SemaineSixJours || politique.ForcerSamediOuvre;

            var fusionnes = SuiviJournalierGrilleHelper.FusionnerMoisCompletPourCalculPaie(
                employeId,
                dateDebut,
                dateFin,
                existantsList,
                semaineSixJours,
                calendrierCtx.Calendrier,
                politique.CompleterJoursSansSaisie,
                politique.ForcerSamediOuvre);

            foreach (var s in fusionnes)
            {
                existants.TryGetValue(s.Date.Date, out var existantDb);
                var ligne = new SuiviJournalierLigne { Date = s.Date };
                ligne.TypeJour = NormaliserTypeJour(existantDb?.TypeJour ?? s.TypeJour);

                if (ligne.TypeJour == SuiviJournalier.TypeNormal && existantDb != null &&
                    !string.IsNullOrEmpty(existantDb.PointagesJson) && !existantDb.HeuresManuelles)
                {
                    var h = PointagesJournalierSerializer.CalculerHeuresLt(existantDb.PointagesJson, s.Date, reglesLt);
                    ligne.InitialiserDepuisDonneesBase(h, false, existantDb.PointagesJson);
                }
                else if (existantDb != null)
                {
                    ligne.InitialiserDepuisDonneesBase(existantDb.HeuresPrestees, existantDb.HeuresManuelles, existantDb.PointagesJson);
                }
                else
                {
                    ligne.InitialiserDepuisDonneesBase(s.HeuresPrestees, false, null);
                }

                Lignes.Add(ligne);
            }

            AbonnerLignesPourTotaux();
            OnPropertyChanged(nameof(TotalHeuresMoisLibelle));
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke($"Erreur chargement : {ex.Message}");
        }
    }

    private void AbonnerLignesPourTotaux()
    {
        foreach (var ligne in Lignes)
            ligne.PropertyChanged -= LigneSuiviPropertyChanged;
        foreach (var ligne in Lignes)
            ligne.PropertyChanged += LigneSuiviPropertyChanged;
    }

    private void LigneSuiviPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SuiviJournalierLigne.HeuresPrestees)
            or nameof(SuiviJournalierLigne.TypeJour)
            or nameof(SuiviJournalierLigne.HeuresManuelles))
            OnPropertyChanged(nameof(TotalHeuresMoisLibelle));
    }

    private static string NormaliserTypeJour(string? typeJour)
    {
        if (string.IsNullOrWhiteSpace(typeJour))
            return SuiviJournalier.TypeNormal;

        return typeJour.Trim() switch
        {
            SuiviJournalier.TypeNormal => SuiviJournalier.TypeNormal,
            SuiviJournalier.TypeCongeAnnuel => SuiviJournalier.TypeCongeAnnuel,
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
            var (_, dateDebut, dateFin) = PeriodePaieHelper.ResoudrePeriode(_db, PeriodeSelectionnee);

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
            ChargerLignes();
            UiFeedback.Succes("Pointage journalier enregistré.");
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
    /// Export PDF de toutes les personnes ayant pointé aujourd'hui (base + terminal, reconnus ou non).
    /// </summary>
    public bool ExporterPointesAujourdhuiPdf(string cheminFichier)
        => ExporterMouvementsJourPdf(cheminFichier, null);

    /// <summary>Export PDF mouvements du jour pour un agent (ligne sélectionnée).</summary>
    public bool ExporterMouvementsJourPdfAgent(string cheminFichier, PresenceEmployeSyntheseLigne? ligne)
    {
        if (ligne == null)
        {
            OnErreur?.Invoke("Sélectionnez un agent dans la liste.");
            return false;
        }

        return ExporterMouvementsJourPdf(cheminFichier, ligne);
    }

    /// <summary>Export PDF du rapport des retards du jour avec coût estimé.</summary>
    public bool ExporterRetardsJourPdf(string cheminFichier)
    {
        if (string.IsNullOrWhiteSpace(cheminFichier))
            return false;

        if (RetardsDuJour.Count == 0)
        {
            OnErreur?.Invoke("Aucun retard enregistré aujourd'hui.");
            return false;
        }

        var aujourdHui = DateTime.Today;
        var lignes = RetardsDuJour
            .OrderBy(x => x.NomComplet, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => new RetardPdfLigne(
                x.Jour,
                x.Matricule,
                x.NomComplet,
                x.Departement,
                x.Entree,
                x.DureeRetardLibelle,
                x.TauxHoraireLibelle,
                x.CoutRetardLibelle,
                x.HeureLimiteLibelle))
            .ToList();

        try
        {
            var service = new ExportPdfService();
            service.ExporterRetardsJourPdf(
                lignes,
                aujourdHui,
                _heureLimiteToleranceLibelle,
                CoutTotalRetardsUsdLibelle,
                CoutTotalRetardsCdfLibelle,
                cheminFichier);
            return File.Exists(cheminFichier);
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke(ex.Message);
            return false;
        }
    }

    private bool ExporterMouvementsJourPdf(string cheminFichier, PresenceEmployeSyntheseLigne? filtreAgent)
    {
        if (string.IsNullOrWhiteSpace(cheminFichier))
            return false;

        var lignesPdf = ConstruireLignesPdfMouvements(filtreAgent);
        if (lignesPdf.Count == 0)
        {
            OnErreur?.Invoke(filtreAgent == null
                ? "Aucun pointage trouvé aujourd'hui. Synchronisez le terminal ou enregistrez des pointages avant l'export."
                : "Aucun mouvement trouvé pour cet agent aujourd'hui.");
            return false;
        }

        var aujourdHui = DateTime.Today;
        var titreAgent = filtreAgent == null
            ? null
            : $"{filtreAgent.NomComplet} ({filtreAgent.Matricule})";
        try
        {
            var service = new ExportPdfService();
            service.ExporterMouvementsJourPdf(
                lignesPdf,
                aujourdHui,
                titreAgent,
                _heureLimiteToleranceLibelle,
                cheminFichier);
            return File.Exists(cheminFichier);
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke(ex.Message);
            return false;
        }
    }

    private List<MouvementJourPdfLigne> ConstruireLignesPdfMouvements(PresenceEmployeSyntheseLigne? filtreAgent)
    {
        IEnumerable<PresenceEmployeSyntheseLigne> source = PresenceSyntheseEmployes;
        if (filtreAgent != null)
            source = source.Where(x => x.Matricule == filtreAgent.Matricule && x.NomComplet == filtreAgent.NomComplet);

        return source
            .OrderBy(x => x.NomComplet, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => new MouvementJourPdfLigne(
                x.Jour,
                x.Matricule,
                x.NomComplet,
                x.Departement,
                x.Entree,
                x.Sortie,
                x.EstRetard ? "En retard" : "À l'heure",
                x.DureeRetardLibelle))
            .ToList();
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
        if (EmployeSelectionne == null || PeriodeSelectionnee == null)
        {
            OnErreur?.Invoke("Sélectionnez d'abord un employé et une période.");
            return;
        }

        var reglesLt = LtServicesReglesProvider.ChargerDepuisDb(_db);
        var nb = 0;
        foreach (var ligne in Lignes)
        {
            if (ligne.TypeJour != SuiviJournalier.TypeNormal)
                continue;
            if (string.IsNullOrEmpty(ligne.PointagesJson))
                continue;
            var h = PointagesJournalierSerializer.CalculerHeuresLt(ligne.PointagesJson, ligne.Date, reglesLt);
            ligne.AppliquerHeuresAutomatiques(h);
            nb++;
        }

        OnPropertyChanged(nameof(TotalHeuresMoisLibelle));
        OnMessageInformation?.Invoke(nb > 0
            ? $"{nb} jour(s) recalculé(s) depuis les pointages terminal (LT)."
            : "Aucun jour avec horodatages à recalculer pour cet employé.");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
