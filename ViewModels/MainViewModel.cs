using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.ViewModels;

/// <summary>
/// Logique de la fenêtre principale : sidebar, stats, liste des employés.
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db = new();
    private int _nbEmployes;
    private decimal _totalMasseSalariale;
    private decimal _totalIprAPayer;
    private int _menuSelectionne;
    private Employe? _employeSelectionne;
    private Employe? _employeSelectionnePourPaie;
    private PeriodePaie? _periodeSelectionneePourPaie;
    private BulletinPaie? _dernierBulletinGenere;
    private PeriodePaie? _periodeSelectionneePourDeclarations;
    private PeriodePaie? _periodeSelectionneePourRapport;
    private int _declarationNbEmployes;
    private decimal _declarationTotalIpr;
    private decimal _declarationTotalCnss;
    private decimal _declarationMasseSalariale;
    private List<Employe> _tousEmployes = new();
    private string _filtreEmployes = "";
    private List<BulletinPaie> _tousBulletinsPeriode = new();
    private string _filtreBulletins = "";
    private string _filtreBulletinsGeneres = "";
    private List<BulletinPaie> _sourceTousBulletinsGenerees = new();
    private readonly DispatcherTimer _filtreEmployesTimer;
    private readonly DispatcherTimer _filtreBulletinsGeneresTimer;
    private readonly DispatcherTimer _filtreRapportPaieTimer;
    private int _bulletinsGeneresTotalCount;
    private int _bulletinsGeneresAfficheCount;
    private decimal _bulletinsGeneresSommeNetListe;
    private List<BulletinPaie> _tousBulletinsRapport = new();
    private string _filtreRapportPaie = "";
    private int _rapportPaieNbBulletins;
    private decimal _rapportPaieTotalBrut;
    private decimal _rapportPaieTotalNet;
    private decimal _rapportPaieTotalIpr;
    private decimal _rapportPaieTotalCnss;
    private BulletinPaie? _bulletinSelectionne;
    private BulletinPaie? _bulletinSelectionnePourRapport;
    private BulletinPaie? _bulletinSelectionnePourCalculPaie;
    private string _dernierMoisTraite = "";
    private decimal _tauxChangeCdfParUsd = ParametresApplicationHelper.TauxParDefaut;
    private string _tauxChangeDerniereMajLibelle = "";
    private bool _syncPeriodesOuvertesAvecTaux = true;
    private string _zkIpText = "";
    private string _zkPortText = "4370";
    private string _zkMachineText = "1";
    private string _zkCommPasswordText = "000000";
    private string _zkIntervalleText = "60";
    private bool _zkSyncActif;
    private DateTime? _zkDerniereSyncUtc;
    private string _zkEtatSyncTexte = "En attente";
    private string _zkEtatSyncCouleur = "#607D8B";
    private string _ltHeureDebutTravailText = "07:30";
    private string _ltHeureLimiteToleranceText = "07:40";
    private string _ltHeureDebutPauseText = "12:00";
    private string _ltHeureFinPauseText = "13:00";
    private string _ltHeureFinSemaineText = "16:00";
    private string _ltHeureFinSamediText = "12:30";
    private string _ltModePointage = LtReglesPointageModes.QuatrePointages;
    private bool _ltDeductionPauseAutomatique = true;
    private string _dashboardDateDuJour = "";
    private int _dashboardPointesAujourdhui;
    private int _dashboardSansPointageAujourdhui;
    private int _dashboardRetardsAujourdhui;
    private int _dashboardSortiesValideesAujourdhui;
    private int _dashboardPausesEnCoursAujourdhui;
    private decimal _dashboardTauxPresenceAujourdhui;
    private string _entrepriseCouranteLibelle = "";
    private int _entrepriseCouranteId;

    public MainViewModel()
    {
        ContexteEntrepriseService.InitialiserDepuisBase(_db);
        _entrepriseCouranteId = ContexteEntrepriseService.ObtenirEntrepriseCouranteId(_db);
        if (_entrepriseCouranteId > 0)
            _db.SetTenant(_entrepriseCouranteId);

        LtModesPointageOptions = new ObservableCollection<LtModePointageOption>(
            LtReglesPointageModes.OptionsUi.Select(o => new LtModePointageOption(o.Code, o.Libelle)));

        Employes = new ObservableCollection<Employe>();
        PeriodesPaie = new ObservableCollection<PeriodePaie>();
        DernierBulletinDetails = new ObservableCollection<BulletinDetail>();
        BulletinsPeriode = new ObservableCollection<BulletinPaie>();
        BulletinsPeriodeCalculPaie = new ObservableCollection<BulletinPaie>();
        BulletinsRapportPaie = new ObservableCollection<BulletinPaie>();
        TousBulletins = new ObservableCollection<BulletinPaie>();
        DashboardMois = new ObservableCollection<DashboardMoisItem>();
        SituationPaieMois = new SituationPaieItem { Libelle = "Aucune période traitée" };
        SituationPaieCumulee = new SituationPaieItem { Libelle = "Depuis le début de l'année" };
        ChecklistMoisPaie = new ObservableCollection<MoisPaieChecklistItem>();
        PointageLiveNotificationService.EtatChange += NotifierBadgePointageMenu;

        static bool PeutMod() => AuthService.PeutModifierDonnees;
        static bool PeutAdmin() => AuthService.PeutAdministrerApplication;

        NouvelEmployeCommand = new RelayCommand(_ => OnOuvrirNouvelEmploye?.Invoke(), _ => PeutMod());
        ModifierEmployeCommand = new RelayCommand(_ => ModifierEmploye(), _ => EmployeSelectionne != null && PeutMod());
        SupprimerEmployeCommand = new RelayCommand(_ => SupprimerEmploye(), _ => EmployeSelectionne != null && PeutMod());
        OuvrirContratsCommand = new RelayCommand(_ => OnOuvrirContrats?.Invoke(EmployeSelectionne!.Id), _ => EmployeSelectionne != null);
        OuvrirAyantsDroitCommand = new RelayCommand(_ => OnOuvrirAyantsDroit?.Invoke(EmployeSelectionne!.Id), _ => EmployeSelectionne != null);
        OuvrirPretsAvancesCommand = new RelayCommand(_ => OnOuvrirPretsAvances?.Invoke(EmployeSelectionne!.Id), _ => EmployeSelectionne != null);
        OuvrirPrimesIndemnitesCommand = new RelayCommand(_ => OnOuvrirPrimesIndemnites?.Invoke(EmployeSelectionne!.Id), _ => EmployeSelectionne != null);
        OuvrirHeuresMoisEmployeCommand = new RelayCommand(_ => OnOuvrirHeuresMoisEmploye?.Invoke(EmployeSelectionne!.Id), _ => EmployeSelectionne != null);
        OuvrirChampsComplementairesEmployeCommand = new RelayCommand(_ => OnOuvrirChampsComplementairesEmploye?.Invoke(), _ => EmployeSelectionne != null);
        OuvrirFormulairesDynamiquesCommand = new RelayCommand(_ => OnOuvrirFormulairesDynamiques?.Invoke());
        OuvrirChampsComplementairesEntrepriseCommand = new RelayCommand(_ => OnOuvrirChampsComplementairesEntreprise?.Invoke());
        RafraichirCommand = new RelayCommand(_ => { ChargerEmployes(); ChargerStatistiques(); });
        SelectionnerMenuCommand = new RelayCommand(p =>
        {
            var menu = ConvertMenuParameter(p);
            MenuSelectionne = menu;
            if (menu == 1)
                PointageLiveNotificationService.ReinitialiserBadge();
            if (menu == 0) { ChargerStatistiques(); ChargerTableauDeBord(); }
            // 1 = Pointage journalier, 2 = Totaux heures (paie)
            if (menu == 4) { ChargerPeriodes(); SelectionnerPremierePeriodeSiVide(); ChargerBulletinsPeriodeCalculPaie(); }
            if (menu == 5)
            {
                ChargerPeriodes();
                if (PeriodeSelectionneePourDeclarations == null && PeriodesPaie.Count > 0)
                    PeriodeSelectionneePourDeclarations = PeriodesPaie[0];
                else
                    ChargerDeclarations();
            }
            if (menu == 7) ChargerTousBulletins();
            if (menu == 8)
            {
                ChargerPeriodes();
                if (PeriodeSelectionneePourRapport == null && PeriodesPaie.Count > 0)
                    PeriodeSelectionneePourRapport = PeriodesPaie[0];
                else
                    ChargerRapportPaie();
            }
            if (menu == 6)
            {
                ChargerTauxChangeDepuisDb();
                ChargerParametresZk();
            }
        });
        OuvrirParametresIprCommand = new RelayCommand(_ => OnOuvrirParametresIpr?.Invoke());
        GenererBulletinCommand = new RelayCommand(_ => GenererBulletin(),
            _ => PeutMod() && PeriodeSelectionneePourPaie != null && !PeriodeSelectionneePourPaie.Cloturee);
        VoirBulletinCalculPaieCommand = new RelayCommand(_ => { if (BulletinSelectionnePourCalculPaie != null) OnVoirBulletin?.Invoke(BulletinSelectionnePourCalculPaie); }, _ => BulletinSelectionnePourCalculPaie != null);
        TelechargerBulletinCalculPaieCommand = new RelayCommand(_ => { if (BulletinSelectionnePourCalculPaie != null) OnTelechargerBulletin?.Invoke(BulletinSelectionnePourCalculPaie); }, _ => BulletinSelectionnePourCalculPaie != null);
        ExportDeclarationCnssCommand = new RelayCommand(_ => ExporterDeclarationCnss());
        ExportDeclarationIprCommand = new RelayCommand(_ => ExporterDeclarationIpr());
        ExportDeclarationCnssExcelCommand = new RelayCommand(_ => OnExporterDeclarationCnssExcel?.Invoke(PeriodeSelectionneePourDeclarations!.Id), _ => PeriodeSelectionneePourDeclarations != null);
        ExportDeclarationIprExcelCommand = new RelayCommand(_ => OnExporterDeclarationIprExcel?.Invoke(PeriodeSelectionneePourDeclarations!.Id), _ => PeriodeSelectionneePourDeclarations != null);
        ExportLivrePaiePdfCommand = new RelayCommand(_ => OnExporterLivrePaiePdf?.Invoke(PeriodeSelectionneePourDeclarations!.Id), _ => PeriodeSelectionneePourDeclarations != null);
        ExportLivrePaieExcelCommand = new RelayCommand(_ => OnExporterLivrePaieExcel?.Invoke(PeriodeSelectionneePourDeclarations!.Id), _ => PeriodeSelectionneePourDeclarations != null);
        ExportCnssEdeclarationCommand = new RelayCommand(_ => ExporterCnssEdeclaration(), _ => PeriodeSelectionneePourDeclarations != null);
        ExportFeuillePaieCnssCommand = new RelayCommand(_ => OnExporterFeuillePaieCnss?.Invoke(PeriodeSelectionneePourDeclarations!.Id), _ => PeriodeSelectionneePourDeclarations != null);
        ExportCnssEdeclarationExcelCommand = new RelayCommand(_ => OnExporterCnssEdeclarationExcel?.Invoke(PeriodeSelectionneePourDeclarations!.Id), _ => PeriodeSelectionneePourDeclarations != null);
        ExportDgiIprCommand = new RelayCommand(_ => ExporterDgiIpr(), _ => PeriodeSelectionneePourDeclarations != null);
        ExportDgiIprExcelCommand = new RelayCommand(_ => OnExporterDgiIprExcel?.Invoke(PeriodeSelectionneePourDeclarations!.Id), _ => PeriodeSelectionneePourDeclarations != null);
        ExportLivreReglementairePdfCommand = new RelayCommand(_ => OnExporterLivreReglementairePdf?.Invoke(PeriodeSelectionneePourDeclarations!.Id), _ => PeriodeSelectionneePourDeclarations != null);
        ExportLivreReglementaireExcelCommand = new RelayCommand(_ => OnExporterLivreReglementaireExcel?.Invoke(PeriodeSelectionneePourDeclarations!.Id), _ => PeriodeSelectionneePourDeclarations != null);
        ExportVirementCommand = new RelayCommand(_ => ExporterVirement(), _ => PeriodeSelectionneePourDeclarations != null);
        OuvrirGuideCnssCommand = new RelayCommand(_ => OnOuvrirGuideCnss?.Invoke());
        OuvrirGuideIprCommand = new RelayCommand(_ => OnOuvrirGuideIpr?.Invoke());
        OuvrirCloturePeriodeCommand = new RelayCommand(_ => OuvrirCloturePeriode(), _ => PeriodeSelectionneePourDeclarations != null);
        OuvrirTauxSociauxCommand = new RelayCommand(_ => OnOuvrirTauxSociaux?.Invoke());
        OuvrirPeriodesPaieCommand = new RelayCommand(_ => OnOuvrirPeriodesPaie?.Invoke());
        OuvrirInfosEntrepriseCommand = new RelayCommand(_ => OnOuvrirInfosEntreprise?.Invoke());
        OuvrirCentreConfigurationCommand = new RelayCommand(_ => OnOuvrirCentreConfiguration?.Invoke());
        OuvrirAssistantConfigurationCommand = new RelayCommand(_ => OnOuvrirAssistantConfiguration?.Invoke());
        CreerNouvelleEntrepriseCommand = new RelayCommand(_ => OnCreerNouvelleEntreprise?.Invoke());
        ForcerAssistantProchainDemarrageCommand = new RelayCommand(_ => OnForcerAssistantProchainDemarrage?.Invoke());
        OuvrirConfigPrimesIndemnitesCommand = new RelayCommand(_ => OnOuvrirConfigPrimesIndemnites?.Invoke());
        OuvrirCalendrierTravailCommand = new RelayCommand(_ => OnOuvrirCalendrierTravail?.Invoke());
        OuvrirEtablissementsDepartementsCommand = new RelayCommand(_ => OnOuvrirEtablissementsDepartements?.Invoke());
        ImporterFicheSalaireExcelCommand = new RelayCommand(_ => OnImporterFicheSalaireExcel?.Invoke(), _ => PeutMod());
        OuvrirGestionUtilisateursCommand = new RelayCommand(_ => OnOuvrirGestionUtilisateurs?.Invoke(), _ => PeutAdmin());
        SauvegarderBaseCommand = new RelayCommand(_ => OnSauvegarderBase?.Invoke(), _ => PeutAdmin());
        RestaurerBaseCommand = new RelayCommand(_ => OnRestaurerBase?.Invoke(), _ => PeutAdmin());
        ReinitialiserApplicationCommand = new RelayCommand(_ => OnReinitialiserApplication?.Invoke(), _ => PeutAdmin());
        DeconnecterCommand = new RelayCommand(_ => OnDemandeDeconnexion?.Invoke());
        OuvrirEtapeChecklistCommand = new RelayCommand(p =>
        {
            if (p is MoisPaieChecklistItem etape)
                NaviguerEtapeChecklist(etape);
        });
        VerifierMiseAJourCommand = new RelayCommand(_ => OnVerifierMiseAJour?.Invoke());
        OuvrirSaisiePaieMoisCommand = new RelayCommand(_ =>
        {
            if (PeriodeSelectionneePourPaie != null)
                OnOuvrirSaisiePaieMois?.Invoke(PeriodeSelectionneePourPaie.Id);
            else
                OnErreurCalculPaie?.Invoke("Sélectionnez d'abord une période de paie.");
        });
        OuvrirSuiviJournalierCommand = new RelayCommand(_ => OnOuvrirSuiviJournalier?.Invoke());
        EnregistrerTauxChangeGlobalCommand = new RelayCommand(_ => EnregistrerTauxChangeGlobal(), _ => PeutMod());
        EnregistrerParametresZkCommand = new RelayCommand(_ => EnregistrerParametresZk(), _ => PeutMod());
        EnregistrerReglesLtCommand = new RelayCommand(_ => EnregistrerReglesLt(), _ => PeutMod());
        SynchroniserTerminalZkCommand = new RelayCommand(_ => SynchroniserTerminalZk(), _ => PeutMod());
        TesterConnexionZkCommand = new RelayCommand(_ => TesterConnexionZk());
        ZktecoSynchronisationService.SynchroEnCours += OnSynchroZkEnCours;
        ZktecoSynchronisationService.SynchroReussie += OnSynchroZkReussieMain;
        ZktecoSynchronisationService.SynchroErreur += OnSynchroZkErreurMain;
        VoirBulletinCommand = new RelayCommand(_ => { if (BulletinSelectionne != null) OnVoirBulletin?.Invoke(BulletinSelectionne); }, _ => BulletinSelectionne != null);
        TelechargerBulletinCommand = new RelayCommand(_ => { if (BulletinSelectionne != null) OnTelechargerBulletin?.Invoke(BulletinSelectionne); }, _ => BulletinSelectionne != null);
        TelechargerTousBulletinsCommand = new RelayCommand(_ => TelechargerTousBulletins(), _ => _sourceTousBulletinsGenerees.Count > 0);
        SupprimerBulletinCommand = new RelayCommand(_ => SupprimerBulletinSelectionne(),
            _ => PeutMod() && BulletinSelectionne != null);
        ActualiserBulletinsGeneresCommand = new RelayCommand(_ => ChargerTousBulletins());
        ExportRapportPaieExcelCommand = new RelayCommand(_ => OnExporterRapportPaieExcel?.Invoke(PeriodeSelectionneePourRapport!.Id), _ => PeriodeSelectionneePourRapport != null);
        VoirRapportBulletinCommand = new RelayCommand(_ => { if (BulletinSelectionnePourRapport != null) OnVoirBulletin?.Invoke(BulletinSelectionnePourRapport); }, _ => BulletinSelectionnePourRapport != null);
        TelechargerRapportBulletinCommand = new RelayCommand(_ => { if (BulletinSelectionnePourRapport != null) OnTelechargerBulletin?.Invoke(BulletinSelectionnePourRapport); }, _ => BulletinSelectionnePourRapport != null);

        var uiDispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _filtreEmployesTimer = new DispatcherTimer(DispatcherPriority.Background, uiDispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(280)
        };
        _filtreEmployesTimer.Tick += (_, _) =>
        {
            _filtreEmployesTimer.Stop();
            ApplyFiltreEmployes();
        };
        _filtreBulletinsGeneresTimer = new DispatcherTimer(DispatcherPriority.Background, uiDispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(280)
        };
        _filtreBulletinsGeneresTimer.Tick += (_, _) =>
        {
            _filtreBulletinsGeneresTimer.Stop();
            ApplyFiltreBulletinsGeneres();
        };
        _filtreRapportPaieTimer = new DispatcherTimer(DispatcherPriority.Background, uiDispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(280)
        };
        _filtreRapportPaieTimer.Tick += (_, _) =>
        {
            _filtreRapportPaieTimer.Stop();
            ApplyFiltreRapportPaie();
        };
    }

    public ObservableCollection<Employe> Employes { get; }
    public ObservableCollection<Entreprise> EntreprisesDisponibles { get; } = new();

    public string EntrepriseCouranteLibelle
    {
        get => _entrepriseCouranteLibelle;
        private set { _entrepriseCouranteLibelle = value ?? ""; OnPropertyChanged(); }
    }

    public int EntrepriseCouranteId
    {
        get => _entrepriseCouranteId;
        set
        {
            if (_entrepriseCouranteId == value) return;
            if (DemanderConfirmationChangementEntreprise != null &&
                !DemanderConfirmationChangementEntreprise(_entrepriseCouranteId, value))
            {
                OnPropertyChanged();
                OnPropertyChanged(nameof(EntrepriseCouranteSelection));
                return;
            }

            AppliquerEntrepriseCourante(value);
        }
    }

    /// <summary>Sélection ComboBox entreprise (évite l'erreur de conversion SelectedValue / chaîne vide).</summary>
    public Entreprise? EntrepriseCouranteSelection
    {
        get => EntreprisesDisponibles.FirstOrDefault(e => e.Id == _entrepriseCouranteId);
        set
        {
            if (value == null || value.Id == _entrepriseCouranteId) return;
            EntrepriseCouranteId = value.Id;
        }
    }

    /// <summary>Confirmation avant changement d'entreprise (fournie par MainWindow).</summary>
    public Func<int, int, bool>? DemanderConfirmationChangementEntreprise { get; set; }

    public ObservableCollection<PeriodePaie> PeriodesPaie { get; }

    public ObservableCollection<BulletinDetail> DernierBulletinDetails { get; }

    public ObservableCollection<BulletinPaie> BulletinsPeriode { get; }

    /// <summary>Bulletins de la période sélectionnée (Calcul de paie).</summary>
    public ObservableCollection<BulletinPaie> BulletinsPeriodeCalculPaie { get; }

    /// <summary>Bulletins affichés dans l’onglet Rapport de paie (filtrés).</summary>
    public ObservableCollection<BulletinPaie> BulletinsRapportPaie { get; }

    /// <summary>Bulletin sélectionné dans la liste Calcul de paie.</summary>
    public BulletinPaie? BulletinSelectionnePourCalculPaie
    {
        get => _bulletinSelectionnePourCalculPaie;
        set { _bulletinSelectionnePourCalculPaie = value; OnPropertyChanged(); (VoirBulletinCalculPaieCommand as RelayCommand)?.RaiseCanExecuteChanged(); (TelechargerBulletinCalculPaieCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    /// <summary>Liste de tous les bulletins générés (onglet Bulletins générés).</summary>
    public ObservableCollection<BulletinPaie> TousBulletins { get; }

    /// <summary>Bulletin sélectionné dans la liste "Bulletins générés".</summary>
    public BulletinPaie? BulletinSelectionne
    {
        get => _bulletinSelectionne;
        set
        {
            _bulletinSelectionne = value;
            OnPropertyChanged();
            (VoirBulletinCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (TelechargerBulletinCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SupprimerBulletinCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Données pour le graphique d'évolution (6 derniers mois) sur le tableau de bord.</summary>
    public ObservableCollection<DashboardMoisItem> DashboardMois { get; }

    /// <summary>Situation de la paie du dernier mois traité.</summary>
    public SituationPaieItem SituationPaieMois { get; }

    /// <summary>Situation de la paie cumulée depuis le début de l'année.</summary>
    public SituationPaieItem SituationPaieCumulee { get; }

    /// <summary>Libellé du dernier mois traité (ex. "mars 2025").</summary>
    public string DernierMoisTraite
    {
        get => _dernierMoisTraite;
        set { _dernierMoisTraite = value ?? ""; OnPropertyChanged(); }
    }

    /// <summary>Recherche dans la liste des employés (matricule, nom, prénom, département).</summary>
    public string FiltreEmployes
    {
        get => _filtreEmployes;
        set
        {
            if (_filtreEmployes == value) return;
            _filtreEmployes = value ?? "";
            OnPropertyChanged();
            _filtreEmployesTimer.Stop();
            _filtreEmployesTimer.Start();
        }
    }

    /// <summary>Filtre la liste des bulletins par employé (matricule, nom, prénom).</summary>
    public string FiltreBulletins
    {
        get => _filtreBulletins;
        set { _filtreBulletins = value ?? ""; OnPropertyChanged(); ApplyFiltreBulletins(); }
    }

    /// <summary>Filtre l’onglet Bulletins générés (matricule, nom, n° bulletin, période…).</summary>
    public string FiltreBulletinsGeneres
    {
        get => _filtreBulletinsGeneres;
        set
        {
            if (_filtreBulletinsGeneres == value) return;
            _filtreBulletinsGeneres = value ?? "";
            OnPropertyChanged();
            _filtreBulletinsGeneresTimer.Stop();
            _filtreBulletinsGeneresTimer.Start();
        }
    }

    /// <summary>Nombre total de bulletins chargés (toute la base).</summary>
    public int BulletinsGeneresTotalCount { get => _bulletinsGeneresTotalCount; private set { _bulletinsGeneresTotalCount = value; OnPropertyChanged(); } }

    /// <summary>Nombre de lignes après filtre.</summary>
    public int BulletinsGeneresAfficheCount { get => _bulletinsGeneresAfficheCount; private set { _bulletinsGeneresAfficheCount = value; OnPropertyChanged(); } }

    /// <summary>Somme des nets à payer sur les lignes affichées (filtre appliqué).</summary>
    public decimal BulletinsGeneresSommeNetListe { get => _bulletinsGeneresSommeNetListe; private set { _bulletinsGeneresSommeNetListe = value; OnPropertyChanged(); } }

    public int NbEmployes
    {
        get => _nbEmployes;
        set { _nbEmployes = value; OnPropertyChanged(); }
    }

    public decimal TotalMasseSalariale
    {
        get => _totalMasseSalariale;
        set { _totalMasseSalariale = value; OnPropertyChanged(); }
    }

    public decimal TotalIprAPayer
    {
        get => _totalIprAPayer;
        set { _totalIprAPayer = value; OnPropertyChanged(); }
    }

    /// <summary>0=Tableau de bord, 1=Pointage, 2=Totaux heures paie, 3=Employés, 4=Calcul paie, 5=Déclarations, 6=Paramètres, 7=Bulletins, 8=Rapport.</summary>
    public int MenuSelectionne
    {
        get => _menuSelectionne;
        set { _menuSelectionne = value; OnPropertyChanged(); OnPropertyChanged(nameof(AfficherContenuGenerique)); }
    }

    /// <summary>True uniquement pour le tableau de bord (0). Paramètres (4) a son propre panneau.</summary>
    public bool AfficherContenuGenerique => MenuSelectionne == 0;

    /// <summary>True si la période sélectionnée pour le calcul de paie est clôturée (génération de bulletins désactivée).</summary>
    public bool PeriodePaieEstCloturee => PeriodeSelectionneePourPaie?.Cloturee ?? false;

    /// <summary>True si l'utilisateur connecté a le rôle Admin (accès à la gestion des utilisateurs).</summary>
    public bool EstAdmin => AuthService.EstAdmin;

    public bool EstLectureSeule => AuthService.EstLectureSeule;

    public bool PeutModifierDonnees => AuthService.PeutModifierDonnees;

    public bool PeutAdministrerApplication => AuthService.PeutAdministrerApplication;

    public ObservableCollection<MoisPaieChecklistItem> ChecklistMoisPaie { get; }

    public string MoisPaieProgression
    {
        get
        {
            if (ChecklistMoisPaie.Count == 0) return "";
            var fait = ChecklistMoisPaie.Count(x => x.EstTermine);
            return $"{fait}/{ChecklistMoisPaie.Count} étapes du mois en cours";
        }
    }

    public int ChecklistEtapesRestantes =>
        ChecklistMoisPaie.Count(x => !x.EstTermine);

    public bool AfficherBadgeChecklist => ChecklistEtapesRestantes > 0;

    public string BadgeChecklistLibelle =>
        ChecklistEtapesRestantes > 9 ? "9+" : ChecklistEtapesRestantes.ToString(CultureInfo.InvariantCulture);

    public bool AfficherBadgePointage => PointageLiveNotificationService.AfficherBadge;

    public string BadgePointageLibelle => PointageLiveNotificationService.BadgeLibelle;

    private void NotifierBadgePointageMenu()
    {
        OnPropertyChanged(nameof(AfficherBadgePointage));
        OnPropertyChanged(nameof(BadgePointageLibelle));
    }

    /// <summary>Recalcule la checklist du mois (léger, sans recharger tout le tableau de bord).</summary>
    public void RafraichirChecklistMoisPaie() => ChargerChecklistMoisPaie();

    public bool AfficherBandeauLectureSeule => EstLectureSeule;

    /// <summary>Texte affichant l'utilisateur connecté (ex. "admin (Administrateur) — Rôle : Admin").</summary>
    public string NomUtilisateurConnecte
    {
        get
        {
            if (!AuthService.EstConnecte || AuthService.UtilisateurCourant == null) return "";
            var u = AuthService.UtilisateurCourant;
            var nom = string.IsNullOrWhiteSpace(u.NomComplet) ? u.Login : $"{u.Login} ({u.NomComplet})";
            return $"{nom} — Rôle : {u.Role}";
        }
    }

    public ICommand NouvelEmployeCommand { get; }
    public ICommand ModifierEmployeCommand { get; }
    public ICommand SupprimerEmployeCommand { get; }
    public ICommand OuvrirContratsCommand { get; }
    public ICommand OuvrirAyantsDroitCommand { get; }
    public ICommand OuvrirPretsAvancesCommand { get; }
    public ICommand OuvrirPrimesIndemnitesCommand { get; }
    public ICommand OuvrirHeuresMoisEmployeCommand { get; }
    public ICommand OuvrirChampsComplementairesEmployeCommand { get; }
    public ICommand OuvrirFormulairesDynamiquesCommand { get; }
    public ICommand OuvrirChampsComplementairesEntrepriseCommand { get; }
    public ICommand RafraichirCommand { get; }
    public ICommand SelectionnerMenuCommand { get; }
    public ICommand OuvrirParametresIprCommand { get; }
    public ICommand GenererBulletinCommand { get; }
    public ICommand VoirBulletinCalculPaieCommand { get; }
    public ICommand TelechargerBulletinCalculPaieCommand { get; }
    public ICommand ExportDeclarationCnssCommand { get; }
    public ICommand ExportDeclarationIprCommand { get; }
    public ICommand ExportDeclarationCnssExcelCommand { get; }
    public ICommand ExportDeclarationIprExcelCommand { get; }
    public ICommand ExportLivrePaiePdfCommand { get; }
    public ICommand ExportLivrePaieExcelCommand { get; }
    public ICommand OuvrirTauxSociauxCommand { get; }
    public ICommand OuvrirPeriodesPaieCommand { get; }
    public ICommand OuvrirInfosEntrepriseCommand { get; }
    public ICommand OuvrirCentreConfigurationCommand { get; }
    public ICommand OuvrirAssistantConfigurationCommand { get; }
    public ICommand CreerNouvelleEntrepriseCommand { get; }
    public ICommand ForcerAssistantProchainDemarrageCommand { get; }
    public ICommand OuvrirConfigPrimesIndemnitesCommand { get; }
    public ICommand OuvrirCalendrierTravailCommand { get; }
    public ICommand OuvrirEtablissementsDepartementsCommand { get; }
    public ICommand ImporterFicheSalaireExcelCommand { get; }
    public ICommand OuvrirGestionUtilisateursCommand { get; }
    public ICommand SauvegarderBaseCommand { get; }
    public ICommand RestaurerBaseCommand { get; }
    public ICommand ReinitialiserApplicationCommand { get; }
    public ICommand VerifierMiseAJourCommand { get; }
    public ICommand VoirBulletinCommand { get; }
    public ICommand TelechargerBulletinCommand { get; }
    public ICommand TelechargerTousBulletinsCommand { get; }
    public ICommand SupprimerBulletinCommand { get; }
    public ICommand ActualiserBulletinsGeneresCommand { get; }
    public ICommand ExportRapportPaieExcelCommand { get; }
    public ICommand VoirRapportBulletinCommand { get; }
    public ICommand TelechargerRapportBulletinCommand { get; }
    public ICommand OuvrirSaisiePaieMoisCommand { get; }
    public ICommand OuvrirSuiviJournalierCommand { get; }
    public ICommand EnregistrerParametresZkCommand { get; }
    public ICommand EnregistrerReglesLtCommand { get; }
    public ICommand SynchroniserTerminalZkCommand { get; }
    public ICommand TesterConnexionZkCommand { get; }
    public ICommand DeconnecterCommand { get; }
    public ICommand OuvrirEtapeChecklistCommand { get; }

    public Action? OnDemandeDeconnexion { get; set; }

    /// <summary>Taux CDF pour 1 USD (paramètre global, onglet Paramètres).</summary>
    public decimal TauxChangeCdfParUsd
    {
        get => _tauxChangeCdfParUsd;
        set { _tauxChangeCdfParUsd = value; OnPropertyChanged(); }
    }

    /// <summary>Texte d'affichage de la dernière mise à jour du taux.</summary>
    public string TauxChangeDerniereMajLibelle
    {
        get => _tauxChangeDerniereMajLibelle;
        private set { _tauxChangeDerniereMajLibelle = value ?? ""; OnPropertyChanged(); }
    }

    /// <summary>Si vrai, l'enregistrement du taux met à jour le champ « taux » des périodes non clôturées.</summary>
    public bool SyncPeriodesOuvertesAvecTaux
    {
        get => _syncPeriodesOuvertesAvecTaux;
        set { _syncPeriodesOuvertesAvecTaux = value; OnPropertyChanged(); }
    }

    public string ZkIpText
    {
        get => _zkIpText;
        set { _zkIpText = value ?? ""; OnPropertyChanged(); }
    }

    public string ZkPortText
    {
        get => _zkPortText;
        set { _zkPortText = value ?? ""; OnPropertyChanged(); }
    }

    public string ZkMachineText
    {
        get => _zkMachineText;
        set { _zkMachineText = value ?? ""; OnPropertyChanged(); }
    }

    public string ZkCommPasswordText
    {
        get => _zkCommPasswordText;
        set { _zkCommPasswordText = value ?? ""; OnPropertyChanged(); }
    }

    public string ZkIntervalleText
    {
        get => _zkIntervalleText;
        set { _zkIntervalleText = value ?? ""; OnPropertyChanged(); }
    }

    public bool ZkSyncActif
    {
        get => _zkSyncActif;
        set { _zkSyncActif = value; OnPropertyChanged(); }
    }

    public string LtHeureDebutTravailText
    {
        get => _ltHeureDebutTravailText;
        set { _ltHeureDebutTravailText = value ?? ""; OnPropertyChanged(); }
    }

    public string LtHeureLimiteToleranceText
    {
        get => _ltHeureLimiteToleranceText;
        set { _ltHeureLimiteToleranceText = value ?? ""; OnPropertyChanged(); }
    }

    public string LtHeureDebutPauseText
    {
        get => _ltHeureDebutPauseText;
        set { _ltHeureDebutPauseText = value ?? ""; OnPropertyChanged(); }
    }

    public string LtHeureFinPauseText
    {
        get => _ltHeureFinPauseText;
        set { _ltHeureFinPauseText = value ?? ""; OnPropertyChanged(); }
    }

    public string LtHeureFinSemaineText
    {
        get => _ltHeureFinSemaineText;
        set { _ltHeureFinSemaineText = value ?? ""; OnPropertyChanged(); }
    }

    public string LtHeureFinSamediText
    {
        get => _ltHeureFinSamediText;
        set { _ltHeureFinSamediText = value ?? ""; OnPropertyChanged(); }
    }

    public ObservableCollection<LtModePointageOption> LtModesPointageOptions { get; }

    public string LtModePointage
    {
        get => _ltModePointage;
        set
        {
            var n = LtReglesPointageModes.Normaliser(value);
            if (_ltModePointage == n) return;
            _ltModePointage = n;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AfficherParametresPause));
            OnPropertyChanged(nameof(AfficherDeductionPauseAutomatique));
            OnPropertyChanged(nameof(LtResumeModePointage));
        }
    }

    public bool LtDeductionPauseAutomatique
    {
        get => _ltDeductionPauseAutomatique;
        set
        {
            if (_ltDeductionPauseAutomatique == value) return;
            _ltDeductionPauseAutomatique = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AfficherParametresPause));
            OnPropertyChanged(nameof(AfficherDeductionPauseAutomatique));
        }
    }

    /// <summary>Affiche les champs pause (obligatoires en mode 4 ; optionnels en mode 2/3 si déduction auto).</summary>
    public bool AfficherParametresPause =>
        LtReglesPointageModes.Normaliser(LtModePointage) != LtReglesPointageModes.DeuxPointages
        || LtDeductionPauseAutomatique;

    /// <summary>Case « déduction pause auto » : pertinente en modes 2 et 3 seulement.</summary>
    public bool AfficherDeductionPauseAutomatique =>
        LtReglesPointageModes.Normaliser(LtModePointage) != LtReglesPointageModes.QuatrePointages;

    public string LtResumeModePointage =>
        LtReglesPointageModes.Normaliser(LtModePointage) switch
        {
            var m when m == LtReglesPointageModes.DeuxPointages =>
                "2 pointages : seules l'entrée et la sortie comptent pour le calcul.",
            var m when m == LtReglesPointageModes.TroisPointages =>
                "3 pointages : entrée, une lecture pause, sortie.",
            _ => "4 pointages : entrée, début pause, fin pause, sortie."
        };

    public string ZkStatutSync =>
        !_zkDerniereSyncUtc.HasValue
            ? "Dernière synchro : —"
            : $"Dernière synchro : {_zkDerniereSyncUtc.Value.ToLocalTime():dd/MM/yyyy HH:mm:ss}";

    public string ZkEtatSyncTexte
    {
        get => _zkEtatSyncTexte;
        private set { _zkEtatSyncTexte = value ?? "En attente"; OnPropertyChanged(); }
    }

    public string ZkEtatSyncCouleur
    {
        get => _zkEtatSyncCouleur;
        private set { _zkEtatSyncCouleur = value ?? "#607D8B"; OnPropertyChanged(); }
    }

    /// <summary>Date affichée pour les métriques opérationnelles du jour.</summary>
    public string DashboardDateDuJour
    {
        get => _dashboardDateDuJour;
        private set { _dashboardDateDuJour = value ?? ""; OnPropertyChanged(); }
    }

    /// <summary>Employés avec au moins un pointage aujourd'hui.</summary>
    public int DashboardPointesAujourdhui
    {
        get => _dashboardPointesAujourdhui;
        private set { _dashboardPointesAujourdhui = value; OnPropertyChanged(); }
    }

    /// <summary>Employés sans pointage aujourd'hui.</summary>
    public int DashboardSansPointageAujourdhui
    {
        get => _dashboardSansPointageAujourdhui;
        private set { _dashboardSansPointageAujourdhui = value; OnPropertyChanged(); }
    }

    /// <summary>Retards détectés selon l'heure limite de tolérance LT.</summary>
    public int DashboardRetardsAujourdhui
    {
        get => _dashboardRetardsAujourdhui;
        private set { _dashboardRetardsAujourdhui = value; OnPropertyChanged(); }
    }

    /// <summary>Employés ayant effectué au moins 4 pointages (cycle complet).</summary>
    public int DashboardSortiesValideesAujourdhui
    {
        get => _dashboardSortiesValideesAujourdhui;
        private set { _dashboardSortiesValideesAujourdhui = value; OnPropertyChanged(); }
    }

    /// <summary>Employés actuellement en pause (2 pointages sans retour).</summary>
    public int DashboardPausesEnCoursAujourdhui
    {
        get => _dashboardPausesEnCoursAujourdhui;
        private set { _dashboardPausesEnCoursAujourdhui = value; OnPropertyChanged(); }
    }

    /// <summary>Taux de présence du jour en %.</summary>
    public decimal DashboardTauxPresenceAujourdhui
    {
        get => _dashboardTauxPresenceAujourdhui;
        private set { _dashboardTauxPresenceAujourdhui = value; OnPropertyChanged(); OnPropertyChanged(nameof(DashboardTauxPresenceAujourdhuiLibelle)); }
    }

    public string DashboardTauxPresenceAujourdhuiLibelle => $"{DashboardTauxPresenceAujourdhui:N1}%";

    public ICommand EnregistrerTauxChangeGlobalCommand { get; }
    public Action<string>? OnMessageZkSettings { get; set; }
    public Action<string>? OnErreurZkSettings { get; set; }

    /// <summary>Succès après enregistrement du taux (MessageBox côté vue).</summary>
    public Action<string>? OnSuccesTauxChange { get; set; }

    /// <summary>Erreur lors de l'enregistrement du taux.</summary>
    public Action<string>? OnErreurTauxChange { get; set; }

    public PeriodePaie? PeriodeSelectionneePourDeclarations
    {
        get => _periodeSelectionneePourDeclarations;
        set
        {
            _periodeSelectionneePourDeclarations = value;
            OnPropertyChanged();
            ChargerDeclarations();
            NotifierExportsDeclarationsCanExecute();
        }
    }

    public PeriodePaie? PeriodeSelectionneePourRapport
    {
        get => _periodeSelectionneePourRapport;
        set
        {
            _periodeSelectionneePourRapport = value;
            OnPropertyChanged();
            ChargerRapportPaie();
            (ExportRapportPaieExcelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Filtre matricule / nom sur la liste du rapport de paie.</summary>
    public string FiltreRapportPaie
    {
        get => _filtreRapportPaie;
        set
        {
            if (_filtreRapportPaie == value) return;
            _filtreRapportPaie = value ?? "";
            OnPropertyChanged();
            _filtreRapportPaieTimer.Stop();
            _filtreRapportPaieTimer.Start();
        }
    }

    public int RapportPaieNbBulletins { get => _rapportPaieNbBulletins; private set { _rapportPaieNbBulletins = value; OnPropertyChanged(); } }
    public decimal RapportPaieTotalBrut { get => _rapportPaieTotalBrut; private set { _rapportPaieTotalBrut = value; OnPropertyChanged(); } }
    public decimal RapportPaieTotalNet { get => _rapportPaieTotalNet; private set { _rapportPaieTotalNet = value; OnPropertyChanged(); } }
    public decimal RapportPaieTotalIpr { get => _rapportPaieTotalIpr; private set { _rapportPaieTotalIpr = value; OnPropertyChanged(); } }
    public decimal RapportPaieTotalCnss { get => _rapportPaieTotalCnss; private set { _rapportPaieTotalCnss = value; OnPropertyChanged(); } }

    public BulletinPaie? BulletinSelectionnePourRapport
    {
        get => _bulletinSelectionnePourRapport;
        set
        {
            _bulletinSelectionnePourRapport = value;
            OnPropertyChanged();
            (VoirRapportBulletinCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (TelechargerRapportBulletinCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public int DeclarationNbEmployes { get => _declarationNbEmployes; set { _declarationNbEmployes = value; OnPropertyChanged(); } }
    public decimal DeclarationTotalIpr { get => _declarationTotalIpr; set { _declarationTotalIpr = value; OnPropertyChanged(); } }
    public decimal DeclarationTotalCnss { get => _declarationTotalCnss; set { _declarationTotalCnss = value; OnPropertyChanged(); } }
    public decimal DeclarationMasseSalariale { get => _declarationMasseSalariale; set { _declarationMasseSalariale = value; OnPropertyChanged(); } }

    /// <summary>Employé sélectionné dans la liste du module Employés (pour Modifier / Supprimer).</summary>
    public Employe? EmployeSelectionne
    {
        get => _employeSelectionne;
        set { _employeSelectionne = value; OnPropertyChanged(); RaiseCommandsEmploye(); }
    }

    public Employe? EmployeSelectionnePourPaie
    {
        get => _employeSelectionnePourPaie;
        set { _employeSelectionnePourPaie = value; OnPropertyChanged(); (GenererBulletinCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    private void RaiseCommandsEmploye()
    {
        (ModifierEmployeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SupprimerEmployeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OuvrirContratsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OuvrirAyantsDroitCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OuvrirPretsAvancesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OuvrirPrimesIndemnitesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OuvrirHeuresMoisEmployeCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void NotifierExportsDeclarationsCanExecute()
    {
        (ExportDeclarationCnssCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportDeclarationIprCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportDeclarationCnssExcelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportDeclarationIprExcelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportLivrePaiePdfCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportLivrePaieExcelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportCnssEdeclarationCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportFeuillePaieCnssCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportCnssEdeclarationExcelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportDgiIprCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportDgiIprExcelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportLivreReglementairePdfCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportLivreReglementaireExcelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportVirementCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OuvrirCloturePeriodeCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public PeriodePaie? PeriodeSelectionneePourPaie
    {
        get => _periodeSelectionneePourPaie;
        set { _periodeSelectionneePourPaie = value; OnPropertyChanged(); OnPropertyChanged(nameof(PeriodePaieEstCloturee)); (GenererBulletinCommand as RelayCommand)?.RaiseCanExecuteChanged(); ChargerBulletinsPeriodeCalculPaie(); }
    }

    public BulletinPaie? DernierBulletinGenere
    {
        get => _dernierBulletinGenere;
        set { _dernierBulletinGenere = value; OnPropertyChanged(); }
    }

    private void ViderDonneesAffichees()
    {
        _tousEmployes.Clear();
        Employes.Clear();
        PeriodesPaie.Clear();
        BulletinsPeriode.Clear();
        BulletinsPeriodeCalculPaie.Clear();
        BulletinsRapportPaie.Clear();
        TousBulletins.Clear();
        _tousBulletinsPeriode.Clear();
        _tousBulletinsRapport.Clear();
        _sourceTousBulletinsGenerees.Clear();
        DashboardMois.Clear();
        EmployeSelectionne = null;
        EmployeSelectionnePourPaie = null;
        PeriodeSelectionneePourPaie = null;
        PeriodeSelectionneePourDeclarations = null;
        PeriodeSelectionneePourRapport = null;
    }

    public void ChargerContexteEntreprise()
    {
        var id = ContexteEntrepriseService.ObtenirEntrepriseCouranteId(_db);

        EntreprisesDisponibles.Clear();
        foreach (var e in _db.Entreprises.IgnoreQueryFilters().AsNoTracking().OrderBy(x => x.RaisonSociale))
            EntreprisesDisponibles.Add(e);

        var totalEmployesBase = _db.Employes.IgnoreQueryFilters().Count();
        if (totalEmployesBase > 0
            && (id <= 0
                || EntreprisesDisponibles.All(e => e.Id != id)
                || TenantDataBackfill.CompterEmployesPourEntreprise(_db, id) == 0))
        {
            id = TenantDataBackfill.HarmoniserEntrepriseActive(_db);
        }
        else if (id <= 0 || EntreprisesDisponibles.All(e => e.Id != id))
        {
            id = EntreprisesDisponibles.FirstOrDefault()?.Id ?? 0;
            if (id > 0)
                ContexteEntrepriseService.DefinirEntrepriseCourante(_db, id);
        }

        if (id > 0 && _db.TenantId != id)
            _db.SetTenant(id);

        _entrepriseCouranteId = id;
        OnPropertyChanged(nameof(EntrepriseCouranteId));
        OnPropertyChanged(nameof(EntrepriseCouranteSelection));

        EntrepriseCouranteLibelle = ContexteEntrepriseService.ObtenirRaisonSocialeCourante(_db)
            ?? "(aucune entreprise)";
    }

    public void ChargerEmployes()
    {
        _tousEmployes = _db.Employes
            .AsNoTracking()
            .Include(x => x.Departement)
            .OrderBy(x => x.Nom)
            .ThenBy(x => x.Prenom)
            .ToList();

        var idsEmployes = _tousEmployes.Select(e => e.Id).ToList();
        Dictionary<int, Contrat> contratsParEmploye = new();
        if (idsEmployes.Count > 0)
        {
            // Contrats : une requête filtrée, dernier contrat par employé en mémoire.
            contratsParEmploye = _db.Contrats.AsNoTracking()
                .Where(c => idsEmployes.Contains(c.EmployeId))
                .OrderByDescending(c => c.DateDebut)
                .ToList()
                .GroupBy(c => c.EmployeId)
                .ToDictionary(g => g.Key, g => g.First());
        }

        var tauxCdfUsd = ParametresApplicationHelper.GetTauxCdfParUsd(_db);
        foreach (var e in _tousEmployes)
        {
            if (!contratsParEmploye.TryGetValue(e.Id, out var c))
            {
                e.SalaireMensuelUsd = e.SalaireMensuelCdf = 0m;
                continue;
            }

            var devise = (c.DeviseBase ?? "USD").Trim().ToUpperInvariant();
            if (devise == "CDF")
            {
                e.SalaireMensuelCdf = c.SalaireBase;
                e.SalaireMensuelUsd = tauxCdfUsd > 0
                    ? decimal.Round(c.SalaireBase / tauxCdfUsd, 4, MidpointRounding.AwayFromZero)
                    : 0m;
            }
            else
            {
                e.SalaireMensuelUsd = c.SalaireBase;
                e.SalaireMensuelCdf = decimal.Round(c.SalaireBase * tauxCdfUsd, 2, MidpointRounding.AwayFromZero);
            }
        }
        _filtreEmployesTimer.Stop();
        ApplyFiltreEmployes();
    }

    private void ApplyFiltreEmployes()
    {
        var filtre = (FiltreEmployes ?? "").Trim().ToLowerInvariant();
        IEnumerable<Employe> source = _tousEmployes;
        if (!string.IsNullOrEmpty(filtre))
        {
            source = _tousEmployes.Where(e =>
            {
                var nomComplet = $"{e.Nom} {e.Postnom} {e.Prenom}".Trim().ToLowerInvariant();
                var dept = e.Departement?.NomDepartement?.ToLowerInvariant() ?? "";
                return (e.Matricule?.ToLowerInvariant().Contains(filtre) == true) ||
                       nomComplet.Contains(filtre) ||
                       dept.Contains(filtre);
            });
        }

        Employes.Clear();
        foreach (var e in source)
            Employes.Add(e);
    }

    public void ChargerPeriodes()
    {
        PeriodesPaie.Clear();
        foreach (var p in _db.PeriodesPaie.AsNoTracking().OrderByDescending(p => p.Annee).ThenByDescending(p => p.Mois))
            PeriodesPaie.Add(p);
    }

    /// <summary>En Calcul de paie, sélectionne la première période si aucune n'est choisie.</summary>
    private void SelectionnerPremierePeriodeSiVide()
    {
        if (PeriodeSelectionneePourPaie != null) return;
        var premiere = PeriodesPaie.FirstOrDefault();
        if (premiere != null)
            PeriodeSelectionneePourPaie = premiere;
    }

    public void ChargerStatistiques()
    {
        var idsEmployes = ContexteEntrepriseService.EmployesEntrepriseCourante(_db)
            .AsNoTracking()
            .Select(e => e.Id)
            .ToList();
        NbEmployes = idsEmployes.Count;
        var contratsActifs = _db.Contrats.AsNoTracking()
            .Where(c => idsEmployes.Contains(c.EmployeId) && (c.DateFin == null || c.DateFin >= DateTime.Today))
            .ToList();
        TotalMasseSalariale = contratsActifs.Sum(c => c.SalaireBase);
        var aujourd = DateTime.Today;
        var bulletinsMois = _db.BulletinsPaie
            .AsNoTracking()
            .Where(b => idsEmployes.Contains(b.EmployeId) &&
                        b.PeriodePaie != null &&
                        b.PeriodePaie.Mois == aujourd.Month &&
                        b.PeriodePaie.Annee == aujourd.Year)
            .ToList();
        TotalIprAPayer = bulletinsMois.Sum(b => b.MontantIprNet);
    }

    /// <summary>À appeler après connexion / déconnexion pour rafraîchir le RBAC UI.</summary>
    public void NotifierChangementSessionUtilisateur()
    {
        OnPropertyChanged(nameof(EstLectureSeule));
        OnPropertyChanged(nameof(PeutModifierDonnees));
        OnPropertyChanged(nameof(AfficherBandeauLectureSeule));
        OnPropertyChanged(nameof(NomUtilisateurConnecte));

        AppSessionEvents.NotifierSessionUtilisateurChanged();

        (NouvelEmployeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ModifierEmployeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SupprimerEmployeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (GenererBulletinCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ImporterFicheSalaireExcelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (EnregistrerTauxChangeGlobalCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (EnregistrerParametresZkCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (EnregistrerReglesLtCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SynchroniserTerminalZkCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OuvrirGestionUtilisateursCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SauvegarderBaseCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RestaurerBaseCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ReinitialiserApplicationCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void ChargerDonnees()
    {
        ChargerContexteEntreprise();
        ChargerEmployes();
        ChargerPeriodes();
        ChargerStatistiques();
        // Tableau de bord : requêtes lourdes — après le premier rendu UI pour éviter un blocage au démarrage
        if (System.Windows.Application.Current?.Dispatcher != null)
            System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ChargerTableauDeBord));
        else
            ChargerTableauDeBord();
    }

    /// <summary>Charge les données du tableau de bord : situation paie du mois et cumulée depuis le début de l'année.</summary>
    public void ChargerTableauDeBord()
    {
        DashboardMois.Clear();
        var anneeCourante = DateTime.Today.Year;

        // Dernière période ayant au moins un bulletin (sans charger tous les bulletins)
        var dernierPeriode = (
            from b in _db.BulletinsPaie.AsNoTracking()
            join p in _db.PeriodesPaie.AsNoTracking() on b.PeriodePaieId equals p.Id
            orderby p.Annee descending, p.Mois descending
            select p).FirstOrDefault();

        var bulletinsMois = new List<BulletinPaie>();
        if (dernierPeriode != null)
        {
            DernierMoisTraite = $"{GetNomMois(dernierPeriode.Mois)} {dernierPeriode.Annee}";
            bulletinsMois = _db.BulletinsPaie
                .AsNoTracking()
                .Where(b => b.PeriodePaieId == dernierPeriode.Id)
                .ToList();
            RemplirSituationPaie(SituationPaieMois, bulletinsMois, DernierMoisTraite, dernierPeriode.Id, null);
        }
        else
        {
            DernierMoisTraite = "Aucune période traitée";
            RemplirSituationPaie(SituationPaieMois, bulletinsMois, DernierMoisTraite, null, null);
        }

        var bulletinsAnnee = (
            from b in _db.BulletinsPaie.AsNoTracking()
            join p in _db.PeriodesPaie.AsNoTracking() on b.PeriodePaieId equals p.Id
            where p.Annee == anneeCourante
            select b).ToList();

        RemplirSituationPaie(SituationPaieCumulee, bulletinsAnnee, $"Cumul {anneeCourante}", null, anneeCourante);
        RemplirGraphiqueEvolutionSixMois();
        ChargerPulseOperationnelDuJour();
        ChargerChecklistMoisPaie();
    }

    /// <summary>Métriques opérationnelles du jour pour le tableau de bord.</summary>
    private void ChargerPulseOperationnelDuJour()
    {
        var aujourdHui = DateTime.Today;
        DashboardDateDuJour = aujourdHui.ToString("dddd dd MMMM yyyy", CultureInfo.GetCultureInfo("fr-FR"));

        var effectif = ContexteEntrepriseService.EmployesEntrepriseCourante(_db).AsNoTracking().Count();
        if (effectif <= 0)
        {
            DashboardPointesAujourdhui = 0;
            DashboardSansPointageAujourdhui = 0;
            DashboardRetardsAujourdhui = 0;
            DashboardSortiesValideesAujourdhui = 0;
            DashboardPausesEnCoursAujourdhui = 0;
            DashboardTauxPresenceAujourdhui = 0m;
            return;
        }

        var reglesLt = LtServicesReglesProvider.ChargerDepuisDb(_db);
        var suivisDuJour = _db.SuivisJournaliers
            .AsNoTracking()
            .Where(s => s.Date.Date == aujourdHui && !string.IsNullOrEmpty(s.PointagesJson) && s.PointagesJson != "[]")
            .Select(s => new { s.EmployeId, s.PointagesJson })
            .ToList();

        var pointes = 0;
        var retards = 0;
        var sortiesValidees = 0;
        var pausesEnCours = 0;

        foreach (var suivi in suivisDuJour)
        {
            var pointages = PointagesJournalierSerializer.Deserialiser(suivi.PointagesJson, aujourdHui)
                .Select(p => p.Kind == DateTimeKind.Utc ? p.ToLocalTime() : DateTime.SpecifyKind(p, DateTimeKind.Local))
                .OrderBy(p => p)
                .ToList();

            if (pointages.Count == 0)
                continue;

            pointes++;
            if (pointages[0].TimeOfDay > reglesLt.HeureLimiteTolerance)
                retards++;
            if (pointages.Count >= reglesLt.NombrePointagesJourComplet)
                sortiesValidees++;
            if (reglesLt.UtiliseQuatrePointages && pointages.Count == 2)
                pausesEnCours++;
        }

        DashboardPointesAujourdhui = pointes;
        DashboardSansPointageAujourdhui = Math.Max(0, effectif - pointes);
        DashboardRetardsAujourdhui = retards;
        DashboardSortiesValideesAujourdhui = sortiesValidees;
        DashboardPausesEnCoursAujourdhui = pausesEnCours;
        DashboardTauxPresenceAujourdhui = effectif == 0 ? 0m : decimal.Round((decimal)pointes * 100m / effectif, 1, MidpointRounding.AwayFromZero);
    }

    /// <summary>Barres d’évolution (6 mois glissants) pour le tableau de bord.</summary>
    private void RemplirGraphiqueEvolutionSixMois()
    {
        var culture = CultureInfo.GetCultureInfo("fr-FR");
        var today = DateTime.Today;
        var debutMois = new DateTime(today.Year, today.Month, 1);
        var points = new List<(DateTime Mois, decimal Masse, decimal Ipr)>();
        decimal maxMasse = 0, maxIpr = 0;

        // SQLite / EF : Sum(decimal) n'est pas traduit — une requête + agrégation en mémoire
        var premierMois = debutMois.AddMonths(-5);
        var dernierMois = debutMois;
        var minCle = premierMois.Year * 100 + premierMois.Month;
        var maxCle = dernierMois.Year * 100 + dernierMois.Month;
        var lignes = _db.BulletinsPaie
            .Where(b => b.PeriodePaie != null
                && b.PeriodePaie.Annee * 100 + b.PeriodePaie.Mois >= minCle
                && b.PeriodePaie.Annee * 100 + b.PeriodePaie.Mois <= maxCle)
            .Select(b => new
            {
                Brut = b.TotalGainImposable + b.TotalGainNonImposable,
                b.MontantIprNet,
                b.PeriodePaie!.Annee,
                b.PeriodePaie.Mois
            })
            .ToList();
        var parPeriode = lignes
            .GroupBy(x => (x.Annee, x.Mois))
            .ToDictionary(g => g.Key, g => (Masse: g.Sum(x => x.Brut), Ipr: g.Sum(x => x.MontantIprNet)));

        for (var i = 5; i >= 0; i--)
        {
            var refMois = debutMois.AddMonths(-i);
            var m = refMois.Month;
            var y = refMois.Year;
            var masse = 0m;
            var ipr = 0m;
            if (parPeriode.TryGetValue((y, m), out var totaux))
            {
                masse = totaux.Masse;
                ipr = totaux.Ipr;
            }
            if (masse > maxMasse) maxMasse = masse;
            if (ipr > maxIpr) maxIpr = ipr;
            points.Add((refMois, masse, ipr));
        }

        if (maxMasse <= 0) maxMasse = 1;
        if (maxIpr <= 0) maxIpr = 1;

        foreach (var (mois, masse, ipr) in points)
        {
            var lib = mois.ToString("MMM yyyy", culture);
            if (lib.Length > 0)
                lib = char.ToUpper(lib[0], culture) + lib.Substring(1);
            DashboardMois.Add(new DashboardMoisItem
            {
                Libelle = lib,
                MasseSalariale = masse,
                IprTotal = ipr,
                BarHeightMasse = 14 + (double)(masse / maxMasse) * 86,
                BarHeightIpr = 14 + (double)(ipr / maxIpr) * 86
            });
        }
    }

    private static string GetNomMois(int mois)
    {
        var noms = new[] { "", "janvier", "février", "mars", "avril", "mai", "juin", "juillet", "août", "septembre", "octobre", "novembre", "décembre" };
        return mois >= 1 && mois <= 12 ? noms[mois] : mois.ToString();
    }

    /// <param name="periodeIdPourIndemnites">Période pour filtrer les lignes de détail (transport / logement) en SQL.</param>
    /// <param name="anneePourIndemnites">Année civile pour le cumul (alternative à la période).</param>
    private void RemplirSituationPaie(SituationPaieItem item, List<BulletinPaie> bulletins, string libelle, int? periodeIdPourIndemnites, int? anneePourIndemnites)
    {
        item.Libelle = libelle;
        item.TotalSalairesNets = bulletins.Sum(b => b.NetAPayer);
        if (periodeIdPourIndemnites.HasValue)
        {
            item.TotalIndemnitesTransport = SommeDetailsGainMotClePourPeriode(periodeIdPourIndemnites.Value, "transport");
            item.TotalIndemnitesLogement = SommeDetailsGainMotClePourPeriode(periodeIdPourIndemnites.Value, "logement");
        }
        else if (anneePourIndemnites.HasValue)
        {
            item.TotalIndemnitesTransport = SommeDetailsGainMotClePourAnnee(anneePourIndemnites.Value, "transport");
            item.TotalIndemnitesLogement = SommeDetailsGainMotClePourAnnee(anneePourIndemnites.Value, "logement");
        }
        else
        {
            item.TotalIndemnitesTransport = 0;
            item.TotalIndemnitesLogement = 0;
        }

        item.TotalIprRetenus = bulletins.Sum(b => b.MontantIprNet);
        item.TotalCnssRetenues = bulletins.Sum(b => b.CotisationCnssOuvrier);
        item.TotalAllocationsFamiliales = bulletins.Sum(b => b.ReductionFamille);
    }

    private decimal SommeDetailsGainMotClePourPeriode(int periodePaieId, string motCle)
    {
        var mot = motCle.Trim().ToLowerInvariant();
        var q = from d in _db.BulletinsDetails.AsNoTracking()
            join b in _db.BulletinsPaie.AsNoTracking() on d.BulletinPaieId equals b.Id
            where b.PeriodePaieId == periodePaieId && d.Libelle != null && d.Libelle.ToLower().Contains(mot)
            select d.Gain;
        // SQLite ne traduit pas Sum sur decimal : filtrage en SQL, somme en mémoire
        return q.ToList().Sum();
    }

    private decimal SommeDetailsGainMotClePourAnnee(int annee, string motCle)
    {
        var mot = motCle.Trim().ToLowerInvariant();
        var q = from d in _db.BulletinsDetails.AsNoTracking()
            join b in _db.BulletinsPaie.AsNoTracking() on d.BulletinPaieId equals b.Id
            join p in _db.PeriodesPaie.AsNoTracking() on b.PeriodePaieId equals p.Id
            where p.Annee == annee && d.Libelle != null && d.Libelle.ToLower().Contains(mot)
            select d.Gain;
        return q.ToList().Sum();
    }

    public void ChargerDeclarations()
    {
        if (PeriodeSelectionneePourDeclarations is null)
        {
            DeclarationNbEmployes = 0;
            DeclarationTotalIpr = 0;
            DeclarationTotalCnss = 0;
            DeclarationMasseSalariale = 0;
            _tousBulletinsPeriode.Clear();
            BulletinsPeriode.Clear();
            return;
        }
        var service = new DeclarationsService(_db);
        var resume = service.GetResumePourPeriode(PeriodeSelectionneePourDeclarations.Id);
        DeclarationNbEmployes = resume.NbEmployes;
        DeclarationTotalIpr = resume.TotalIprNet;
        DeclarationTotalCnss = resume.TotalCnssOuvrier;
        DeclarationMasseSalariale = resume.MasseSalariale;
        _tousBulletinsPeriode = resume.Bulletins.OrderBy(b => b.Employe?.Matricule).ToList();
        ApplyFiltreBulletins();
    }

    private void ApplyFiltreBulletins()
    {
        var filtre = (FiltreBulletins ?? "").Trim().ToLowerInvariant();
        BulletinsPeriode.Clear();
        foreach (var b in _tousBulletinsPeriode)
        {
            if (string.IsNullOrEmpty(filtre))
            {
                BulletinsPeriode.Add(b);
                continue;
            }
            var e = b.Employe;
            var nomComplet = e != null ? $"{e.Matricule} {e.Nom} {e.Postnom} {e.Prenom}".Trim().ToLowerInvariant() : "";
            if (nomComplet.Contains(filtre))
                BulletinsPeriode.Add(b);
        }
    }

    /// <summary>Charge les bulletins et totaux pour l’onglet Rapport de paie.</summary>
    public void ChargerRapportPaie()
    {
        if (PeriodeSelectionneePourRapport is null)
        {
            RapportPaieNbBulletins = 0;
            RapportPaieTotalBrut = 0;
            RapportPaieTotalNet = 0;
            RapportPaieTotalIpr = 0;
            RapportPaieTotalCnss = 0;
            _tousBulletinsRapport.Clear();
            BulletinsRapportPaie.Clear();
            BulletinSelectionnePourRapport = null;
            return;
        }

        var service = new DeclarationsService(_db);
        var resume = service.GetResumePourPeriode(PeriodeSelectionneePourRapport.Id);
        RapportPaieNbBulletins = resume.NbEmployes;
        RapportPaieTotalIpr = resume.TotalIprNet;
        RapportPaieTotalCnss = resume.TotalCnssOuvrier;
        RapportPaieTotalBrut = resume.Bulletins.Sum(b => b.TotalGainImposable + b.TotalGainNonImposable);
        RapportPaieTotalNet = resume.Bulletins.Sum(b => b.NetAPayer);
        _tousBulletinsRapport = TrierBulletinsParPeriodeEtEmploye(resume.Bulletins);
        _filtreRapportPaieTimer.Stop();
        ApplyFiltreRapportPaie();
    }

    private void ApplyFiltreRapportPaie()
    {
        var filtre = (FiltreRapportPaie ?? "").Trim().ToLowerInvariant();
        var prev = BulletinSelectionnePourRapport;
        List<BulletinPaie> affiches;
        if (string.IsNullOrEmpty(filtre))
        {
            affiches = _tousBulletinsRapport;
        }
        else
        {
            affiches = new List<BulletinPaie>(_tousBulletinsRapport.Count / 4 + 8);
            foreach (var b in _tousBulletinsRapport)
            {
                var e = b.Employe;
                var nomComplet = e != null ? $"{e.Matricule} {e.Nom} {e.Postnom} {e.Prenom}".Trim().ToLowerInvariant() : "";
                var dept = e?.Departement?.NomDepartement?.ToLowerInvariant() ?? "";
                var num = (b.NumeroBulletin ?? b.Id.ToString(CultureInfo.InvariantCulture)).ToLowerInvariant();
                var haystack = $"{nomComplet} {dept} {num}".Trim();
                if (haystack.Contains(filtre))
                    affiches.Add(b);
            }
        }

        BulletinsRapportPaie.Clear();
        foreach (var b in affiches)
            BulletinsRapportPaie.Add(b);

        if (prev != null && BulletinsRapportPaie.Contains(prev))
            BulletinSelectionnePourRapport = prev;
        else
            BulletinSelectionnePourRapport = null;
    }

    /// <summary>Charge les bulletins de la période sélectionnée pour le Calcul de paie.</summary>
    public void ChargerBulletinsPeriodeCalculPaie()
    {
        BulletinsPeriodeCalculPaie.Clear();
        BulletinSelectionnePourCalculPaie = null;
        if (PeriodeSelectionneePourPaie is null) return;
        var bulletins = _db.BulletinsPaie
            .AsNoTracking()
            .Include(b => b.Employe)
            .Include(b => b.PeriodePaie)
            .Where(b => b.PeriodePaieId == PeriodeSelectionneePourPaie.Id)
            .OrderBy(b => b.NumeroBulletin ?? b.Id.ToString())
            .ToList();
        foreach (var b in bulletins)
            BulletinsPeriodeCalculPaie.Add(b);
    }

    /// <summary>Recharge la liste complète des bulletins (onglet Bulletins générés).</summary>
    public void ChargerTousBulletins()
    {
        _sourceTousBulletinsGenerees = _db.BulletinsPaie
            .AsNoTracking()
            .AsSplitQuery()
            .Include(b => b.Employe)
            .ThenInclude(e => e!.Departement)
            .Include(b => b.PeriodePaie)
            .ToList();
        _sourceTousBulletinsGenerees = TrierBulletinsParPeriodeEtEmploye(_sourceTousBulletinsGenerees);
        BulletinsGeneresTotalCount = _sourceTousBulletinsGenerees.Count;
        _filtreBulletinsGeneresTimer.Stop();
        ApplyFiltreBulletinsGeneres();
        (TelechargerTousBulletinsCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void ApplyFiltreBulletinsGeneres()
    {
        var filtre = (FiltreBulletinsGeneres ?? "").Trim().ToLowerInvariant();
        var prev = BulletinSelectionne;
        List<BulletinPaie> affiches;
        if (string.IsNullOrEmpty(filtre))
        {
            affiches = _sourceTousBulletinsGenerees;
        }
        else
        {
            affiches = new List<BulletinPaie>(_sourceTousBulletinsGenerees.Count / 4 + 8);
            foreach (var b in _sourceTousBulletinsGenerees)
            {
                var e = b.Employe;
                var periode = b.PeriodePaie;
                var periodeStr = periode != null ? $"{periode.Mois:D2}/{periode.Annee}" : "";
                var nomComplet = e != null ? $"{e.Matricule} {e.Nom} {e.Postnom} {e.Prenom}".Trim().ToLowerInvariant() : "";
                var dept = e?.Departement?.NomDepartement?.ToLowerInvariant() ?? "";
                var num = (b.NumeroBulletin ?? b.Id.ToString(CultureInfo.InvariantCulture)).ToLowerInvariant();
                var haystack = $"{nomComplet} {dept} {periodeStr} {num}".Trim();
                if (haystack.Contains(filtre))
                    affiches.Add(b);
            }
        }

        TousBulletins.Clear();
        foreach (var b in affiches)
            TousBulletins.Add(b);

        BulletinsGeneresAfficheCount = affiches.Count;
        BulletinsGeneresSommeNetListe = affiches.Sum(x => x.NetAPayer);
        if (prev != null && TousBulletins.Contains(prev))
            BulletinSelectionne = prev;
        else
            BulletinSelectionne = null;
    }

    private static List<BulletinPaie> TrierBulletinsParPeriodeEtEmploye(IEnumerable<BulletinPaie> source)
    {
        return source
            .OrderByDescending(b => b.PeriodePaie?.Annee ?? 0)
            .ThenByDescending(b => b.PeriodePaie?.Mois ?? 0)
            .ThenBy(b => b.Employe?.Matricule ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(b => b.NumeroBulletin ?? b.Id.ToString(CultureInfo.InvariantCulture), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void SupprimerBulletinSelectionne()
    {
        if (BulletinSelectionne is null) return;

        var b = BulletinSelectionne;
        var cfg = ConfigurationExportsPaieService.Obtenir(_db);
        if (cfg.Cloture.BloquerSuppressionBulletinSiCloturee &&
            b.PeriodePaie?.Cloturee == true)
        {
            OnErreurCalculPaie?.Invoke("Impossible de supprimer : la période est clôturée. Rouvrez-la depuis « Périodes de paie » si nécessaire.");
            return;
        }
        if (b.PeriodePaieId > 0 && PeriodeClotureService.PeriodeEstVerrouillee(_db, b.PeriodePaieId) &&
            cfg.Cloture.BloquerSuppressionBulletinSiCloturee)
        {
            OnErreurCalculPaie?.Invoke("Impossible de supprimer : la période est clôturée.");
            return;
        }
        var employe = $"{b.Employe?.Nom} {b.Employe?.Postnom} {b.Employe?.Prenom}".Trim();
        var periode = b.PeriodePaie != null ? $"{b.PeriodePaie.Mois:D2}/{b.PeriodePaie.Annee}" : "N/A";
        var numero = string.IsNullOrWhiteSpace(b.NumeroBulletin) ? b.Id.ToString() : b.NumeroBulletin;

        var confirm = System.Windows.MessageBox.Show(
            $"Supprimer le bulletin {numero} ({employe} - {periode}) ?\n\nCette action est définitive.",
            "Supprimer un bulletin",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (confirm != System.Windows.MessageBoxResult.Yes)
            return;

        try
        {
            var entite = _db.BulletinsPaie.FirstOrDefault(x => x.Id == b.Id);
            if (entite == null)
            {
                OnErreurCalculPaie?.Invoke("Bulletin introuvable ou déjà supprimé.");
                ChargerTousBulletins();
                return;
            }

            var periodeId = entite.PeriodePaieId;
            var maintenance = new BulletinMaintenanceService(_db);
            var supprimes = maintenance.SupprimerBulletins(new[] { b.Id });
            if (supprimes == 0)
            {
                OnErreurCalculPaie?.Invoke("Bulletin introuvable ou déjà supprimé.");
                ChargerTousBulletins();
                return;
            }

            ChargerTousBulletins();
            ChargerTableauDeBord();
            ChargerStatistiques();

            if (PeriodeSelectionneePourDeclarations?.Id == periodeId)
                ChargerDeclarations();
            if (PeriodeSelectionneePourRapport?.Id == periodeId)
                ChargerRapportPaie();
            if (PeriodeSelectionneePourPaie?.Id == periodeId)
                ChargerBulletinsPeriodeCalculPaie();

            OnSuccessCalculPaie?.Invoke("Bulletin supprimé avec succès.");
        }
        catch (Exception ex)
        {
            OnErreurCalculPaie?.Invoke($"Impossible de supprimer le bulletin : {ex.Message}");
        }
    }

    private void TelechargerTousBulletins()
    {
        if (_sourceTousBulletinsGenerees.Count == 0)
        {
            OnErreurCalculPaie?.Invoke("Aucun bulletin à télécharger.");
            return;
        }

        OnTelechargerTousBulletins?.Invoke(_sourceTousBulletinsGenerees.ToList());
    }

    private void ExporterDeclarationCnss()
    {
        if (PeriodeSelectionneePourDeclarations is null)
        {
            OnErreurCalculPaie?.Invoke("Veuillez sélectionner une période.");
            return;
        }
        try
        {
            var service = new DeclarationsService(_db);
            var csv = service.ExporterDeclarationCnssCsv(PeriodeSelectionneePourDeclarations.Id);
            var nom = $"Declaration_CNSS_{PeriodeSelectionneePourDeclarations.Mois}_{PeriodeSelectionneePourDeclarations.Annee}.csv";
            OnExporterFichier?.Invoke(csv, nom);
        }
        catch (Exception ex)
        {
            OnErreurCalculPaie?.Invoke(ex.Message);
        }
    }

    private void ExporterDeclarationIpr()
    {
        if (PeriodeSelectionneePourDeclarations is null)
        {
            OnErreurCalculPaie?.Invoke("Veuillez sélectionner une période.");
            return;
        }
        try
        {
            var service = new DeclarationsService(_db);
            var csv = service.ExporterDeclarationIprCsv(PeriodeSelectionneePourDeclarations.Id);
            var nom = $"Declaration_IPR_{PeriodeSelectionneePourDeclarations.Mois}_{PeriodeSelectionneePourDeclarations.Annee}.csv";
            OnExporterFichier?.Invoke(csv, nom);
        }
        catch (Exception ex)
        {
            OnErreurCalculPaie?.Invoke(ex.Message);
        }
    }

    /// <summary>Convertit CommandParameter (souvent string en XAML) en index de menu.</summary>
    private static int ConvertMenuParameter(object? parameter)
    {
        if (parameter is int i) return i;
        if (parameter is string s && int.TryParse(s, out var parsed)) return parsed;
        return 0;
    }

    public Action? OnOuvrirNouvelEmploye { get; set; }

    /// <summary>Ouvrir la fenêtre de modification pour l'employé dont l'id est passé. Après fermeture (succès), la vue peut appeler ChargerEmployes.</summary>
    public Action<int>? OnOuvrirModifierEmploye { get; set; }

    /// <summary>Ouvrir la fenêtre des contrats pour l'employé dont l'id est passé.</summary>
    public Action<int>? OnOuvrirContrats { get; set; }

    /// <summary>Ouvrir la fenêtre des ayants droit pour l'employé dont l'id est passé.</summary>
    public Action<int>? OnOuvrirAyantsDroit { get; set; }

    public Action<int>? OnOuvrirPretsAvances { get; set; }
    public Action<int>? OnOuvrirPrimesIndemnites { get; set; }
    public Action<int>? OnOuvrirHeuresMoisEmploye { get; set; }
    public Action? OnOuvrirChampsComplementairesEmploye { get; set; }
    public Action? OnOuvrirFormulairesDynamiques { get; set; }
    public Action? OnOuvrirChampsComplementairesEntreprise { get; set; }

    public Action? OnOuvrirParametresIpr { get; set; }
    public Action? OnOuvrirTauxSociaux { get; set; }
    public Action? OnOuvrirPeriodesPaie { get; set; }
    public Action? OnOuvrirInfosEntreprise { get; set; }
    public Action? OnOuvrirCentreConfiguration { get; set; }
    public Action? OnOuvrirAssistantConfiguration { get; set; }
    public Action? OnCreerNouvelleEntreprise { get; set; }
    public Action? OnForcerAssistantProchainDemarrage { get; set; }
    public Action? OnOuvrirConfigPrimesIndemnites { get; set; }
    public Action? OnOuvrirEtablissementsDepartements { get; set; }
    public Action? OnImporterFicheSalaireExcel { get; set; }
    public Action? OnOuvrirGestionUtilisateurs { get; set; }
    public Action? OnSauvegarderBase { get; set; }
    public Action? OnRestaurerBase { get; set; }
    public Action? OnReinitialiserApplication { get; set; }
    public Action? OnVerifierMiseAJour { get; set; }

    public string VersionApplication =>
        ApplicationUpdateService.FormaterVersion(ApplicationUpdateService.ObtenirVersionInstallee());
    public Action? OnOuvrirCalendrierTravail { get; set; }

    /// <summary>Ouvre le formulaire de saisie de paie pour une période (tous les employés).</summary>
    public Action<int>? OnOuvrirSaisiePaieMois { get; set; }
    public Action? OnOuvrirSuiviJournalier { get; set; }

    /// <summary>
    /// Message d'erreur pour le calcul de paie (affiché par la vue).
    /// </summary>
    public Action<string>? OnErreurCalculPaie { get; set; }

    /// <summary>
    /// Message de succès (ex. "X bulletins générés pour la période.").
    /// </summary>
    public Action<string>? OnSuccessCalculPaie { get; set; }

    /// <summary>
    /// Demande à la vue d'ouvrir l'écran de visualisation du bulletin.
    /// </summary>
    public Action<string, string>? OnExporterFichier { get; set; }

    /// <summary>Export livre de paie PDF : la vue affiche SaveFileDialog puis génère le PDF pour la période (id).</summary>
    public Action<int>? OnExporterLivrePaiePdf { get; set; }

    /// <summary>Export livre de paie Excel : la vue affiche SaveFileDialog puis génère le fichier pour la période (id).</summary>
    public Action<int>? OnExporterLivrePaieExcel { get; set; }

    /// <summary>Export rapport de paie Excel : la vue affiche SaveFileDialog puis génère le fichier pour la période (id).</summary>
    public Action<int>? OnExporterRapportPaieExcel { get; set; }

    /// <summary>Ouvre la fenêtre de visualisation d’un bulletin (liste des bulletins générés).</summary>
    public Action<BulletinPaie>? OnVoirBulletin { get; set; }

    /// <summary>Ouvre SaveFileDialog et exporte le bulletin en PDF.</summary>
    public Action<BulletinPaie>? OnTelechargerBulletin { get; set; }

    /// <summary>Ouvre un sélecteur de dossier et exporte tous les bulletins en PDF.</summary>
    public Action<List<BulletinPaie>>? OnTelechargerTousBulletins { get; set; }

    /// <summary>Export déclaration CNSS Excel : la vue affiche SaveFileDialog puis génère le fichier pour la période (id).</summary>
    public Action<int>? OnExporterDeclarationCnssExcel { get; set; }

    /// <summary>Export déclaration IPR Excel : la vue affiche SaveFileDialog puis génère le fichier pour la période (id).</summary>
    public Action<int>? OnExporterDeclarationIprExcel { get; set; }

    public Action<int>? OnExporterCnssEdeclarationExcel { get; set; }
    public Action<int>? OnExporterFeuillePaieCnss { get; set; }
    public Action<int>? OnExporterDgiIprExcel { get; set; }
    public Action<int>? OnExporterLivreReglementairePdf { get; set; }
    public Action<int>? OnExporterLivreReglementaireExcel { get; set; }
    public Action? OnOuvrirGuideCnss { get; set; }
    public Action? OnOuvrirGuideIpr { get; set; }
    public Action<int>? OnOuvrirCloturePeriode { get; set; }

    public ICommand ExportCnssEdeclarationCommand { get; }
    public ICommand ExportFeuillePaieCnssCommand { get; }
    public ICommand ExportCnssEdeclarationExcelCommand { get; }
    public ICommand ExportDgiIprCommand { get; }
    public ICommand ExportDgiIprExcelCommand { get; }
    public ICommand ExportLivreReglementairePdfCommand { get; }
    public ICommand ExportLivreReglementaireExcelCommand { get; }
    public ICommand ExportVirementCommand { get; }
    public ICommand OuvrirGuideCnssCommand { get; }
    public ICommand OuvrirGuideIprCommand { get; }
    public ICommand OuvrirCloturePeriodeCommand { get; }

    private void ExporterCnssEdeclaration()
    {
        if (PeriodeSelectionneePourDeclarations is null) return;
        try
        {
            var profil = ConfigurationExportsPaieService.Obtenir(_db).ExportCnssEdeclaration;
            if (string.Equals(profil.TypeFormat, "Excel", StringComparison.OrdinalIgnoreCase))
            {
                OnExporterCnssEdeclarationExcel?.Invoke(PeriodeSelectionneePourDeclarations.Id);
                return;
            }

            var svc = new CnssEDeclarationExportService(_db);
            var csv = svc.ExporterCsv(PeriodeSelectionneePourDeclarations.Id);
            OnExporterFichier?.Invoke(csv, svc.ObtenirNomFichierSuggere(PeriodeSelectionneePourDeclarations));
        }
        catch (Exception ex) { OnErreurCalculPaie?.Invoke(ex.Message); }
    }

    private void ExporterDgiIpr()
    {
        if (PeriodeSelectionneePourDeclarations is null) return;
        try
        {
            var svc = new DgiIprDeclarationExportService(_db);
            var csv = svc.ExporterCsv(PeriodeSelectionneePourDeclarations.Id);
            OnExporterFichier?.Invoke(csv, svc.ObtenirNomFichierSuggere(PeriodeSelectionneePourDeclarations));
        }
        catch (Exception ex) { OnErreurCalculPaie?.Invoke(ex.Message); }
    }

    private void ExporterVirement()
    {
        if (PeriodeSelectionneePourDeclarations is null) return;
        try
        {
            var svc = new VirementBancaireExportService(_db);
            var csv = svc.ExporterCsv(PeriodeSelectionneePourDeclarations.Id);
            OnExporterFichier?.Invoke(csv, svc.ObtenirNomFichierSuggere(PeriodeSelectionneePourDeclarations, null));
        }
        catch (Exception ex) { OnErreurCalculPaie?.Invoke(ex.Message); }
    }

    private void OuvrirCloturePeriode()
    {
        if (PeriodeSelectionneePourDeclarations is null) return;
        OnOuvrirCloturePeriode?.Invoke(PeriodeSelectionneePourDeclarations.Id);
    }

    private void ModifierEmploye()
    {
        if (EmployeSelectionne is null) return;
        OnOuvrirModifierEmploye?.Invoke(EmployeSelectionne.Id);
    }

    private void SupprimerEmploye()
    {
        if (EmployeSelectionne is null) return;
        var nom = $"{EmployeSelectionne.Nom} {EmployeSelectionne.Prenom}".Trim();
        var confirm = System.Windows.MessageBox.Show(
            $"Supprimer l'employé \"{nom}\" (matricule {EmployeSelectionne.Matricule}) ?\n\nLes ayant-droits seront supprimés. L'opération échouera si des contrats ou bulletins sont liés.",
            "Confirmer la suppression",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;
        try
        {
            var id = EmployeSelectionne.Id;

            // Empêche la suppression si des contrats ou bulletins existent déjà pour cet employé.
            if (_db.Contrats.Any(c => c.EmployeId == id))
            {
                OnErreurCalculPaie?.Invoke(
                    "Impossible de supprimer cet employé : au moins un contrat est encore lié.\n\n" +
                    "Allez dans « Gérer les contrats » pour clôturer ou supprimer les contrats, puis réessayez.");
                return;
            }

            if (_db.BulletinsPaie.Any(b => b.EmployeId == id))
            {
                OnErreurCalculPaie?.Invoke(
                    "Impossible de supprimer cet employé : des bulletins de paie existent déjà pour lui.\n\n" +
                    "Supprimez d'abord les bulletins (ou conservez l'employé pour l'historique), puis réessayez.");
                return;
            }

            var entite = _db.Employes.Find(id);
            if (entite != null)
            {
                _db.Employes.Remove(entite);
                _db.SaveChanges();
            }
            EmployeSelectionne = null;
            ChargerEmployes();
            ChargerStatistiques();
        }
        catch (Exception ex)
        {
            OnErreurCalculPaie?.Invoke($"Impossible de supprimer : {ex.Message}");
        }
    }

    private void ChargerTauxChangeDepuisDb()
    {
        ParametresApplicationHelper.EnsureRow(_db);
        TauxChangeCdfParUsd = ParametresApplicationHelper.GetTauxCdfParUsd(_db);
        var p = ParametresApplicationHelper.GetParametresEntrepriseCourante(_db);
        TauxChangeDerniereMajLibelle = p?.DateDerniereModification is { } dt
            ? $"Dernière modification : {DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToLocalTime():g}"
            : "";
    }

    private void ChargerParametresZk()
    {
        var p = ParametresApplicationHelper.GetParametresEntrepriseCourante(_db);

        _zkIpText = p.ZkTerminalIp ?? "";
        _zkPortText = p.ZkTerminalPort > 0 ? p.ZkTerminalPort.ToString() : "4370";
        _zkMachineText = p.ZkMachineNumber > 0 ? p.ZkMachineNumber.ToString() : "1";
        _zkIntervalleText = p.ZkIntervalleSecondes > 0 ? p.ZkIntervalleSecondes.ToString() : "60";
        _zkCommPasswordText = p.ZkCommPassword == 0 ? "000000" : p.ZkCommPassword.ToString(CultureInfo.InvariantCulture);
        _zkSyncActif = p.ZkSyncActif;
        _zkDerniereSyncUtc = p.ZkDerniereSyncUtc;
        _ltHeureDebutTravailText = p.LtHeureDebutTravail;
        _ltHeureLimiteToleranceText = p.LtHeureLimiteTolerance;
        _ltHeureDebutPauseText = p.LtHeureDebutPause;
        _ltHeureFinPauseText = p.LtHeureFinPause;
        _ltHeureFinSemaineText = p.LtHeureFinSemaine;
        _ltHeureFinSamediText = p.LtHeureFinSamedi;
        _ltModePointage = LtReglesPointageModes.Normaliser(p.LtModePointage);
        _ltDeductionPauseAutomatique = p.LtDeductionPauseAutomatique;

        OnPropertyChanged(nameof(ZkIpText));
        OnPropertyChanged(nameof(ZkPortText));
        OnPropertyChanged(nameof(ZkMachineText));
        OnPropertyChanged(nameof(ZkIntervalleText));
        OnPropertyChanged(nameof(ZkCommPasswordText));
        OnPropertyChanged(nameof(ZkSyncActif));
        OnPropertyChanged(nameof(ZkStatutSync));
        OnPropertyChanged(nameof(LtHeureDebutTravailText));
        OnPropertyChanged(nameof(LtHeureLimiteToleranceText));
        OnPropertyChanged(nameof(LtHeureDebutPauseText));
        OnPropertyChanged(nameof(LtHeureFinPauseText));
        OnPropertyChanged(nameof(LtHeureFinSemaineText));
        OnPropertyChanged(nameof(LtHeureFinSamediText));
        OnPropertyChanged(nameof(LtModePointage));
        OnPropertyChanged(nameof(LtDeductionPauseAutomatique));
        OnPropertyChanged(nameof(AfficherParametresPause));
        OnPropertyChanged(nameof(AfficherDeductionPauseAutomatique));
        OnPropertyChanged(nameof(LtResumeModePointage));
    }

    private void EnregistrerParametresZk()
    {
        try
        {
            DefinirEtatSync("Sync en cours...", "#FB8C00");
            if (!EssayerParserParametresZk(out var port, out var machine, out var intervalle, out var commPwd, out var err))
            {
                DefinirEtatSync("Sync erreur", "#C62828");
                OnErreurZkSettings?.Invoke(err ?? "Paramètres ZKTeco invalides.");
                return;
            }

            if (ZkSyncActif && string.IsNullOrWhiteSpace(ZkIpText))
            {
                DefinirEtatSync("Sync erreur", "#C62828");
                OnErreurZkSettings?.Invoke("Pour activer la synchronisation automatique, renseignez l’adresse IP du terminal.");
                return;
            }

            var p = ParametresApplicationHelper.GetParametresEntrepriseCourante(_db);

            p.ZkTerminalIp = string.IsNullOrWhiteSpace(ZkIpText) ? null : ZkIpText.Trim();
            p.ZkTerminalPort = port;
            p.ZkMachineNumber = machine;
            p.ZkCommPassword = commPwd;
            p.ZkIntervalleSecondes = intervalle;
            p.ZkSyncActif = ZkSyncActif;
            _db.SaveChanges();
            ZktecoSynchronisationService.Reconfigurer();
            ZkTerminalParametresNotifier.Raise(this);
            ChargerParametresZk();
            DefinirEtatSync("Sync OK", "#2E7D32");
            OnMessageZkSettings?.Invoke("Paramètres ZKTeco enregistrés.");
        }
        catch (Exception ex)
        {
            DefinirEtatSync("Sync erreur", "#C62828");
            OnErreurZkSettings?.Invoke(ex.Message);
        }
    }

    private void SynchroniserTerminalZk()
    {
        try
        {
            DefinirEtatSync("Sync en cours...", "#FB8C00");
            EnregistrerParametresZk();
            if (!ZktecoSynchronisationService.TrySynchroniser(out var err))
            {
                DefinirEtatSync("Sync erreur", "#C62828");
                OnErreurZkSettings?.Invoke(err ?? "Échec de la synchronisation ZKTeco.");
                return;
            }

            ChargerParametresZk();
            DefinirEtatSync("Sync OK", "#2E7D32");
            OnMessageZkSettings?.Invoke("Synchronisation ZKTeco terminée.");
        }
        catch (Exception ex)
        {
            DefinirEtatSync("Sync erreur", "#C62828");
            OnErreurZkSettings?.Invoke(ex.Message);
        }
    }

    private void TesterConnexionZk()
    {
        try
        {
            DefinirEtatSync("Sync en cours...", "#FB8C00");
            if (!EssayerParserParametresZk(out var port, out var machine, out _, out var commPwd, out var err))
            {
                DefinirEtatSync("Sync erreur", "#C62828");
                OnErreurZkSettings?.Invoke(err ?? "Paramètres invalides.");
                return;
            }

            var ip = ZkIpText.Trim();
            var logs = ZktecoPointageReader.Lire(ip, port, machine, commPwd);
            DefinirEtatSync("Sync OK", "#2E7D32");
            OnMessageZkSettings?.Invoke($"Connexion réussie. {logs.Count} enregistrement(s) lu(s) dans le journal du terminal.");
        }
        catch (Exception ex)
        {
            DefinirEtatSync("Sync erreur", "#C62828");
            OnErreurZkSettings?.Invoke(ex.Message);
        }
    }

    private void OnSynchroZkEnCours() => DefinirEtatSync("Sync en cours...", "#FB8C00");

    private void OnSynchroZkReussieMain(DateTime _)
    {
        DefinirEtatSync("Sync OK", "#2E7D32");
        AppSessionEvents.NotifierDonneesMetierModifiees();
    }

    private void OnSynchroZkErreurMain(string _) => DefinirEtatSync("Sync erreur", "#C62828");

    private void DefinirEtatSync(string texte, string couleurHex)
    {
        ZkEtatSyncTexte = texte;
        ZkEtatSyncCouleur = couleurHex;
    }

    private bool EssayerParserParametresZk(out int port, out int machine, out int intervalle, out int commPassword, out string? erreur)
    {
        port = 4370;
        machine = 1;
        intervalle = 60;
        commPassword = 0;
        erreur = null;

        if (string.IsNullOrWhiteSpace(ZkIpText))
        {
            erreur = "Indiquez l’adresse IP du terminal.";
            return false;
        }

        if (!int.TryParse(ZkPortText.Trim(), out port) || port <= 0 || port > 65535)
        {
            erreur = "Port invalide (1–65535, souvent 4370).";
            return false;
        }

        if (!int.TryParse(ZkMachineText.Trim(), out machine) || machine <= 0)
        {
            erreur = "Numéro de machine invalide (>= 1, souvent 1).";
            return false;
        }

        var commTx = ZkCommPasswordText.Trim();
        if (string.IsNullOrEmpty(commTx))
            commPassword = 0;
        else if (!int.TryParse(commTx, NumberStyles.Integer, CultureInfo.InvariantCulture, out commPassword) || commPassword < 0)
        {
            erreur = "Clé comm invalide (chiffres uniquement, ex. 000000).";
            return false;
        }

        if (!int.TryParse(ZkIntervalleText.Trim(), out intervalle) || intervalle < 5 || intervalle > 3600)
        {
            erreur = "Intervalle : entre 5 et 3600 secondes.";
            return false;
        }

        return true;
    }

    private void EnregistrerReglesLt()
    {
        try
        {
            if (!ValiderFormatHeure(LtHeureDebutTravailText) ||
                !ValiderFormatHeure(LtHeureLimiteToleranceText) ||
                !ValiderFormatHeure(LtHeureFinSemaineText) ||
                !ValiderFormatHeure(LtHeureFinSamediText))
            {
                OnErreurZkSettings?.Invoke("Renseignez les horaires au format HH:mm (ex. 07:30).");
                return;
            }

            if (AfficherParametresPause &&
                (!ValiderFormatHeure(LtHeureDebutPauseText) || !ValiderFormatHeure(LtHeureFinPauseText)))
            {
                OnErreurZkSettings?.Invoke("Renseignez les horaires de pause au format HH:mm.");
                return;
            }

            if (TimeSpan.Parse(LtHeureLimiteToleranceText) < TimeSpan.Parse(LtHeureDebutTravailText))
            {
                OnErreurZkSettings?.Invoke("L'heure limite de tolérance doit être après le début de service.");
                return;
            }

            if (AfficherParametresPause &&
                TimeSpan.Parse(LtHeureFinPauseText) <= TimeSpan.Parse(LtHeureDebutPauseText))
            {
                OnErreurZkSettings?.Invoke("L'heure de fin de pause doit être après l'heure de début de pause.");
                return;
            }

            var p = ParametresApplicationHelper.GetParametresEntrepriseCourante(_db);

            p.LtHeureDebutTravail = NormaliserHeure(LtHeureDebutTravailText);
            p.LtHeureLimiteTolerance = NormaliserHeure(LtHeureLimiteToleranceText);
            p.LtHeureDebutPause = NormaliserHeure(LtHeureDebutPauseText);
            p.LtHeureFinPause = NormaliserHeure(LtHeureFinPauseText);
            p.LtHeureFinSemaine = NormaliserHeure(LtHeureFinSemaineText);
            p.LtHeureFinSamedi = NormaliserHeure(LtHeureFinSamediText);
            p.LtModePointage = LtReglesPointageModes.Normaliser(LtModePointage);
            p.LtDeductionPauseAutomatique = LtDeductionPauseAutomatique;
            p.DateDerniereModification = DateTime.UtcNow;
            _db.SaveChanges();
            ChargerParametresZk();
            var nbRecalc = SuiviJournalierRecalculService.RecalculerAutomatiqueEntrepriseCourante(_db);
            var suffixe = nbRecalc > 0
                ? $" {nbRecalc} jour(s) de suivi recalculé(s) avec le nouveau mode."
                : "";
            var msg =
                "Règles de service enregistrées pour cette entreprise. Les calculs de pointage utiliseront ce mode." + suffixe;
            AppNotificationService.Succes(msg);
            OnMessageZkSettings?.Invoke(msg);
        }
        catch (Exception ex)
        {
            OnErreurZkSettings?.Invoke(ex.Message);
        }
    }

    private static bool ValiderFormatHeure(string? heure)
    {
        if (string.IsNullOrWhiteSpace(heure))
            return false;
        return TimeSpan.TryParse(heure, out _);
    }

    private static string NormaliserHeure(string heure)
    {
        var t = TimeSpan.Parse(heure);
        return t.ToString(@"hh\:mm");
    }

    private void EnregistrerTauxChangeGlobal()
    {
        try
        {
            if (TauxChangeCdfParUsd <= 0)
            {
                OnErreurTauxChange?.Invoke("Le taux doit être supérieur à 0 (CDF pour 1 USD).");
                return;
            }

            ParametresApplicationHelper.SetTauxCdfParUsd(_db, TauxChangeCdfParUsd, SyncPeriodesOuvertesAvecTaux);
            ChargerTauxChangeDepuisDb();
            ChargerEmployes();
            OnSuccesTauxChange?.Invoke(
                SyncPeriodesOuvertesAvecTaux
                    ? "Taux enregistré. Les périodes de paie non clôturées ont été alignées sur ce taux."
                    : "Taux enregistré. Les nouveaux calculs de bulletins utiliseront cette valeur.");
        }
        catch (Exception ex)
        {
            OnErreurTauxChange?.Invoke(ex.Message);
        }
    }

    private void GenererBulletin()
    {
        if (PeriodeSelectionneePourPaie is null)
        {
            OnErreurCalculPaie?.Invoke("Veuillez sélectionner une période de paie.");
            return;
        }

        try
        {
            var service = new CalculPaieService(_db);
            var (generes, erreurs) = service.GenererBulletinsPourTous(PeriodeSelectionneePourPaie.Id);

            if (erreurs.Count > 0)
            {
                var msgErreurs = string.Join(Environment.NewLine, erreurs);
                OnErreurCalculPaie?.Invoke(
                    generes > 0
                        ? $"{generes} bulletin(s) généré(s). Erreurs pour certains employés :{Environment.NewLine}{msgErreurs}"
                        : $"Aucun bulletin généré. Erreurs :{Environment.NewLine}{msgErreurs}");
            }

            if (generes > 0)
            {
                OnSuccessCalculPaie?.Invoke($"{generes} bulletin(s) généré(s) pour la période {PeriodeSelectionneePourPaie.Mois:D2}/{PeriodeSelectionneePourPaie.Annee}.");
                ChargerBulletinsPeriodeCalculPaie();
                ChargerTousBulletins();
                if (PeriodeSelectionneePourRapport != null && PeriodeSelectionneePourRapport.Id == PeriodeSelectionneePourPaie.Id)
                    ChargerRapportPaie();
                AppSessionEvents.NotifierDonneesMetierModifiees();
            }
        }
        catch (Exception ex)
        {
            var msg = ex.Message.Contains("période est clôturée", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("clôturée", StringComparison.OrdinalIgnoreCase)
                ? "Cette période est clôturée. Vous ne pouvez plus générer de bulletins pour elle. Déclôturez la période dans Paramètres > Périodes de paie si nécessaire."
                : ex.Message;
            OnErreurCalculPaie?.Invoke(msg);
        }
    }

    private void AppliquerEntrepriseCourante(int entrepriseId)
    {
        _entrepriseCouranteId = entrepriseId;
        _db.SetTenant(entrepriseId);
        ContexteEntrepriseService.DefinirEntrepriseCourante(_db, entrepriseId);
        OnPropertyChanged(nameof(EntrepriseCouranteId));
        OnPropertyChanged(nameof(EntrepriseCouranteSelection));
        ViderDonneesAffichees();
        ChargerContexteEntreprise();
        ChargerDonnees();
        AppSessionEvents.NotifierEntrepriseCouranteChanged();
        AppNotificationService.Afficher($"Entreprise active : {EntrepriseCouranteLibelle}", NotificationKind.Info);
    }

    private void ChargerChecklistMoisPaie()
    {
        ChecklistMoisPaie.Clear();
        var aujourdHui = DateTime.Today;
        var periodeMois = _db.PeriodesPaie.AsNoTracking()
            .FirstOrDefault(p => p.Mois == aujourdHui.Month && p.Annee == aujourdHui.Year);
        var periodeMoisId = periodeMois?.Id;

        var idsEmployes = ContexteEntrepriseService.EmployesEntrepriseCourante(_db)
            .AsNoTracking()
            .Select(e => e.Id)
            .ToList();
        var nbEmployes = idsEmployes.Count;

        var debutMois = new DateTime(aujourdHui.Year, aujourdHui.Month, 1);
        var finMois = debutMois.AddMonths(1);
        var nbSuivisPointes = idsEmployes.Count == 0
            ? 0
            : _db.SuivisJournaliers.AsNoTracking()
                .Count(s => idsEmployes.Contains(s.EmployeId)
                            && s.Date >= debutMois && s.Date < finMois
                            && s.PointagesJson != null && s.PointagesJson != "" && s.PointagesJson != "[]");

        var nbBulletins = periodeMois == null
            ? 0
            : _db.BulletinsPaie.AsNoTracking().Count(b => b.PeriodePaieId == periodeMois.Id);

        var prochaineEtapeAssignee = false;

        void Ajouter(int numero, string libelle, string detail, bool termine, int menu, Action? action = null)
        {
            var estProchaine = !termine && !prochaineEtapeAssignee;
            if (estProchaine)
                prochaineEtapeAssignee = true;

            ChecklistMoisPaie.Add(new MoisPaieChecklistItem
            {
                Numero = numero,
                Libelle = libelle,
                Detail = detail,
                EstTermine = termine,
                EstProchaineEtape = estProchaine,
                MenuCible = menu,
                OuvrirCommand = new RelayCommand(_ =>
                {
                    if (action != null)
                        action();
                    else
                        MenuSelectionne = menu;
                    if (menu == 0)
                        ChargerTableauDeBord();
                })
            });
        }

        Ajouter(1, "Période de paie du mois",
            periodeMois != null ? $"{GetNomMois(aujourdHui.Month)} {aujourdHui.Year}" : "À créer dans Périodes de paie",
            periodeMois != null, 6, () => OnOuvrirPeriodesPaie?.Invoke());
        Ajouter(2, "Effectif enregistré",
            nbEmployes > 0 ? $"{nbEmployes} employé(s)" : "Ajoutez au moins un employé",
            nbEmployes > 0, 3);
        Ajouter(3, "Pointages du mois",
            nbSuivisPointes > 0 ? $"{nbSuivisPointes} jour(s) pointé(s)" : "Synchronisez le terminal ou saisissez les heures",
            nbSuivisPointes > 0, 1);
        Ajouter(4, "Bulletins calculés",
            nbBulletins > 0 ? $"{nbBulletins} bulletin(s)" : "Lancez le calcul de paie",
            nbBulletins > 0, 4);
        Ajouter(5, "Déclarations & exports",
            nbBulletins > 0 ? "CNSS / IPR disponibles après calcul" : "Après génération des bulletins",
            nbBulletins > 0, 5);
        Ajouter(6, "Clôture de période",
            periodeMois?.Cloturee == true ? "Période verrouillée" : "À faire en fin de mois",
            periodeMois?.Cloturee == true, 5, () =>
            {
                if (periodeMoisId is int id)
                    OnOuvrirCloturePeriode?.Invoke(id);
                else if (PeriodeSelectionneePourDeclarations != null)
                    OnOuvrirCloturePeriode?.Invoke(PeriodeSelectionneePourDeclarations.Id);
                else
                    OnOuvrirPeriodesPaie?.Invoke();
            });

        OnPropertyChanged(nameof(MoisPaieProgression));
        OnPropertyChanged(nameof(ChecklistEtapesRestantes));
        OnPropertyChanged(nameof(AfficherBadgeChecklist));
        OnPropertyChanged(nameof(BadgeChecklistLibelle));
    }

    private void NaviguerEtapeChecklist(MoisPaieChecklistItem etape)
    {
        if (etape.OuvrirCommand?.CanExecute(null) == true)
            etape.OuvrirCommand.Execute(null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
