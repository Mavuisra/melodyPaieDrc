using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.ViewModels;

/// <summary>Ligne du tableau des totaux d’heures pour un employé sur une période.</summary>
public sealed class HeuresTotauxEmployeRow
{
    public int EmployeId { get; init; }
    public string Matricule { get; init; } = "";
    public string NomComplet { get; init; } = "";
    public string? Departement { get; init; }
    public decimal TotalHeures { get; init; }
    public decimal TotalJoursEquivalent { get; init; }

    public string TotalHeuresLibelle =>
        TotalHeures.ToString("N2", CultureInfo.CurrentCulture) + " h";

    public string TotalJoursEquivalentLibelle =>
        TotalJoursEquivalent.ToString("N2", CultureInfo.CurrentCulture) + " j";
}

/// <summary>Cellule du calendrier mensuel (aperçu des heures pour l’employé sélectionné).</summary>
public sealed class CalendrierJourCellVm
{
    public DateTime Date { get; init; }
    public int NumeroJour { get; init; }
    public bool EstDansMoisVisible { get; init; }
    public bool EstAujourdhui { get; init; }
    public bool EstWeekEnd { get; init; }
    public string HeuresCourtLibelle { get; init; } = "";
    public int NiveauActivite { get; init; }
    public bool EstSelectionne { get; init; }
    public string TypeJourBadge { get; init; } = "";
    public string TypeJourBadgeCouleurFond { get; init; } = "#E2E8F0";
    public string TypeJourBadgeCouleurTexte { get; init; } = "#334155";
    public bool AfficherTypeJourBadge => !string.IsNullOrWhiteSpace(TypeJourBadge);
}

/// <summary>Totaux d’heures issues du suivi journalier (pointage / saisie) pour une période de paie.</summary>
public sealed class HeuresPresteesTotauxViewModel : INotifyPropertyChanged
{
    private static readonly CultureInfo Fr = new("fr-FR");

    private readonly PaieDbContext _db;
    private PeriodePaie? _periodeSelectionnee;
    private decimal _totalGeneralHeures;
    private decimal _totalGeneralJoursEquivalent;
    private Employe? _employeSelectionne;
    private int _moisCalendrier = DateTime.Today.Month;
    private int _anneeCalendrier = DateTime.Today.Year;
    private DateTime? _dateSelectionnee;
    private DetailJourEmployeVm? _detailJour;

    private ICommand? _moisPrecedentCommand;
    private ICommand? _moisSuivantCommand;
    private ICommand? _selectionnerJourCommand;
    private ICommand? _enregistrerMomentsCommand;
    private ICommand? _enregistrerTypeJourCommand;
    private ICommand? _actualiserTotauxCommand;
    private ICommand? _exporterRapportAgentCommand;
    private ICommand? _exporterRapportQuinzainesCommand;
    private ICommand? _exporterRapportMensuelCommand;
    private ICommand? _exporterHeuresPeriodeCommand;
    private ICommand? _exporterHeuresEmployeCommand;

    private readonly HeuresPaieRapportService _rapportPaieService = new();

    public HeuresPresteesTotauxViewModel(PaieDbContext db)
    {
        _db = db;
        PeriodesPaie = new ObservableCollection<PeriodePaie>();
        Lignes = new ObservableCollection<HeuresTotauxEmployeRow>();
        LignesSituationPaie = new ObservableCollection<SituationPaieAgentLigne>();
        Employes = new ObservableCollection<Employe>();
        CellulesCalendrier = new ObservableCollection<CalendrierJourCellVm>();
        LignesPointagesJour = new ObservableCollection<PointageAffichageLtDto>();
        ChargerPeriodes();
        ChargerEmployes();
    }

    public decimal HeuresParJourEquivalent
    {
        get
        {
            var entrepriseId = ContexteEntrepriseService.ObtenirEntrepriseCouranteId(_db);
            var h = new PolitiquePaieService(_db).Charger(entrepriseId).HeuresParJour;
            return h > 0m ? h : SalaireReferenceHelper.HeuresDefaut;
        }
    }

    public string JoursEquivalentAideLibelle =>
        "Jours équivalents : heures ÷ nominal du jour (8 h lun.–ven., 5 h sam. si semaine 6 jours) — même formule que le calcul de paie.";

    public ObservableCollection<PeriodePaie> PeriodesPaie { get; }

    public ObservableCollection<HeuresTotauxEmployeRow> Lignes { get; }

    public ObservableCollection<SituationPaieAgentLigne> LignesSituationPaie { get; }

    /// <summary>Affiche la section rapports paie lorsqu'une période est active.</summary>
    public bool AfficherSectionRapports => PeriodeSelectionnee != null && LignesSituationPaie.Count > 0;

    public ObservableCollection<Employe> Employes { get; }

    public ObservableCollection<CalendrierJourCellVm> CellulesCalendrier { get; }

    /// <summary>Lecture seule : une ligne par horodatage du jour sélectionné (employé + date).</summary>
    public ObservableCollection<PointageAffichageLtDto> LignesPointagesJour { get; }

    /// <summary>Faux tant qu’il n’y a pas de période exploitable : afficher le message d’aide à la place du tableau.</summary>
    public bool AfficherTableau => PeriodesPaie.Count > 0 && PeriodeSelectionnee != null;

    /// <summary>Vue calendrier et détail jour lorsqu’un employé est choisi.</summary>
    public bool AfficherCalendrierEmploye => AfficherTableau && EmployeSelectionne != null;

    /// <summary>Tableau récap tous employés (période) — toujours visible lorsqu'une période est sélectionnée.</summary>
    public bool AfficherTableauRecapEmployesPeriode => AfficherTableau;

    /// <summary>Tableau des pointages uniquement pour le jour sélectionné (employé obligatoire).</summary>
    public bool AfficherTableauPointagesJourSelectionne =>
        AfficherTableau && EmployeSelectionne != null && _dateSelectionnee.HasValue;

    public string TitreTableauBas
    {
        get
        {
            if (!AfficherTableau || EmployeSelectionne == null || !_dateSelectionnee.HasValue)
                return "";

            var e = EmployeSelectionne;
            var nom = $"{e.Nom} {e.Postnom} {e.Prenom}".Trim();
            var d = _dateSelectionnee.Value;
            return $"Pointages du {d.ToString("dddd d MMMM yyyy", Fr)} — {e.Matricule} · {nom}";
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
            OnPropertyChanged(nameof(AfficherTableau));
            OnPropertyChanged(nameof(AfficherCalendrierEmploye));
            OnPropertyChanged(nameof(PériodeLibellé));
            ChargerTotaux();
            SyncCalendrierAvecPeriode();
            NotifyModeTableauBas();
        }
    }

    public Employe? EmployeSelectionne
    {
        get => _employeSelectionne;
        set
        {
            if (_employeSelectionne == value) return;
            _employeSelectionne = value;
            _dateSelectionnee = null;
            DetailJour = null;
            LignesPointagesJour.Clear();
            OnPropertyChanged();
            OnPropertyChanged(nameof(AfficherCalendrierEmploye));
            OnPropertyChanged(nameof(TotalHeuresMoisAfficheLibelle));
            OnPropertyChanged(nameof(TotalJoursMoisAfficheLibelle));
            ConstruireGrilleCalendrier();
            NotifyModeTableauBas();
            (ExporterRapportAgentCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ExporterHeuresEmployeCommand as RelayCommand)?.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(SituationEmployeSelectionne));
        }
    }

    public string PériodeLibellé =>
        PeriodeSelectionnee == null
            ? "—"
            : $"{CultureInfo.GetCultureInfo("fr-FR").DateTimeFormat.GetMonthName(PeriodeSelectionnee.Mois)} {PeriodeSelectionnee.Annee}";

    public string MoisCalendrierLibelle
    {
        get
        {
            try
            {
                var s = new DateTime(_anneeCalendrier, _moisCalendrier, 1).ToString("MMMM yyyy", Fr);
                return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s);
            }
            catch
            {
                return "—";
            }
        }
    }

    public string TotalHeuresMoisAfficheLibelle
    {
        get
        {
            if (EmployeSelectionne == null) return "—";
            var t = SuiviJournalierPdfDataService.CalculerTotalHeuresPourEmploye(_db, EmployeSelectionne.Id, _moisCalendrier, _anneeCalendrier);
            return t.ToString("N2", CultureInfo.CurrentCulture) + " h";
        }
    }

    public string TotalJoursMoisAfficheLibelle
    {
        get
        {
            if (EmployeSelectionne == null) return "—";
            var j = SuiviJournalierPdfDataService.CalculerJoursEquivalentsPourEmploye(
                _db, EmployeSelectionne.Id, _moisCalendrier, _anneeCalendrier);
            return j.ToString("N2", CultureInfo.CurrentCulture) + " j";
        }
    }

    public decimal TotalGeneralHeures
    {
        get => _totalGeneralHeures;
        private set { if (_totalGeneralHeures == value) return; _totalGeneralHeures = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalGeneralHeuresLibelle)); }
    }

    public string TotalGeneralHeuresLibelle =>
        TotalGeneralHeures.ToString("N2", CultureInfo.CurrentCulture) + " h";

    public decimal TotalGeneralJoursEquivalent
    {
        get => _totalGeneralJoursEquivalent;
        private set
        {
            if (_totalGeneralJoursEquivalent == value) return;
            _totalGeneralJoursEquivalent = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TotalGeneralJoursEquivalentLibelle));
        }
    }

    public string TotalGeneralJoursEquivalentLibelle =>
        TotalGeneralJoursEquivalent.ToString("N2", CultureInfo.CurrentCulture) + " j";

    public DetailJourEmployeVm? DetailJour
    {
        get => _detailJour;
        private set
        {
            _detailJour = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(AfficherPanelDetail));
            if (_enregistrerMomentsCommand is RelayCommand r)
                r.RaiseCanExecuteChanged();
            if (_enregistrerTypeJourCommand is RelayCommand rt)
                rt.RaiseCanExecuteChanged();
        }
    }

    public bool AfficherPanelDetail => DetailJour != null;

    public string MessageVide =>
        PeriodesPaie.Count == 0
            ? "Créez d’abord des périodes de paie dans Paramètres → Périodes de paie."
            : PeriodeSelectionnee == null
                ? "Sélectionnez une période de paie."
                : "";

    public ICommand MoisPrecedentCommand =>
        _moisPrecedentCommand ??= new RelayCommand(_ => MoisPrecedent(), _ => AfficherTableau);

    public ICommand MoisSuivantCommand =>
        _moisSuivantCommand ??= new RelayCommand(_ => MoisSuivant(), _ => AfficherTableau);

    public ICommand SelectionnerJourCommand =>
        _selectionnerJourCommand ??= new RelayCommand(p => SelectionnerJour(p));

    public ICommand EnregistrerMomentsCommand =>
        _enregistrerMomentsCommand ??= new RelayCommand(
            _ => EnregistrerMoments(),
            _ => DroitsUi.PeutModifier && DetailJour?.PeutEditerMoments == true);

    public ICommand EnregistrerTypeJourCommand =>
        _enregistrerTypeJourCommand ??= new RelayCommand(
            _ => EnregistrerTypeJour(),
            _ => DroitsUi.PeutModifier && DetailJour != null && EmployeSelectionne != null && _dateSelectionnee.HasValue);

    public ICommand ActualiserTotauxCommand =>
        _actualiserTotauxCommand ??= new RelayCommand(_ => RafraichirTotaux());

    public ICommand ExporterRapportAgentCommand =>
        _exporterRapportAgentCommand ??= new RelayCommand(
            _ => OnDemandeExportRapportAgent?.Invoke(),
            _ => EmployeSelectionne != null && PeriodeSelectionnee != null);

    public ICommand ExporterRapportQuinzainesCommand =>
        _exporterRapportQuinzainesCommand ??= new RelayCommand(
            _ => OnDemandeExportRapportQuinzaines?.Invoke(),
            _ => PeriodeSelectionnee != null && LignesSituationPaie.Count > 0);

    public ICommand ExporterRapportMensuelCommand =>
        _exporterRapportMensuelCommand ??= new RelayCommand(
            _ => OnDemandeExportRapportMensuel?.Invoke(),
            _ => PeriodeSelectionnee != null && LignesSituationPaie.Count > 0);

    public ICommand ExporterHeuresPeriodeCommand =>
        _exporterHeuresPeriodeCommand ??= new RelayCommand(
            _ => OnDemandeExportHeuresPeriode?.Invoke(),
            _ => PeriodeSelectionnee != null && Lignes.Count > 0);

    public ICommand ExporterHeuresEmployeCommand =>
        _exporterHeuresEmployeCommand ??= new RelayCommand(
            _ => OnDemandeExportHeuresEmploye?.Invoke(),
            _ => PeriodeSelectionnee != null && EmployeSelectionne != null);

    public event Action? OnDemandeExportRapportAgent;
    public event Action? OnDemandeExportRapportQuinzaines;
    public event Action? OnDemandeExportRapportMensuel;
    public event Action? OnDemandeExportHeuresPeriode;
    public event Action? OnDemandeExportHeuresEmploye;

    public void ChargerPeriodes()
    {
        var selectedId = PeriodeSelectionnee?.Id;
        PeriodesPaie.Clear();
        foreach (var p in _db.PeriodesPaie.OrderByDescending(x => x.Annee).ThenByDescending(x => x.Mois))
            PeriodesPaie.Add(p);

        if (PeriodesPaie.Count == 0)
        {
            PeriodeSelectionnee = null;
            return;
        }

        var nouvelle = selectedId.HasValue
            ? PeriodesPaie.FirstOrDefault(x => x.Id == selectedId.Value) ?? PeriodesPaie[0]
            : PeriodesPaie[0];

        PeriodeSelectionnee = nouvelle;
    }

    public void RafraichirTotaux()
    {
        ChargerEmployes();
        ChargerPeriodes();
    }

    public void RechargerPourEntrepriseCourante() => RafraichirTotaux();

    /// <summary>Recalcule totaux, calendrier et détail jour après changement du mode de pointage LT.</summary>
    public void RafraichirApresChangementReglesLt()
    {
        ChargerTotaux();
        ConstruireGrilleCalendrier();
        OnPropertyChanged(nameof(TotalHeuresMoisAfficheLibelle));
        OnPropertyChanged(nameof(TotalJoursMoisAfficheLibelle));
        if (_dateSelectionnee.HasValue && EmployeSelectionne != null)
            RafraichirDetailJour();
    }

    public void NotifierDroitsModification()
    {
        (EnregistrerMomentsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (EnregistrerTypeJourCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void ChargerEmployes()
    {
        var idConserve = EmployeSelectionne?.Id;
        Employes.Clear();
        foreach (var e in ContexteEntrepriseService.EmployesEntrepriseCourante(_db).AsNoTracking().Include(x => x.Departement).OrderBy(x => x.Matricule))
            Employes.Add(e);

        if (idConserve.HasValue)
            EmployeSelectionne = Employes.FirstOrDefault(x => x.Id == idConserve.Value);
    }

    private void SyncCalendrierAvecPeriode()
    {
        _dateSelectionnee = null;
        DetailJour = null;
        LignesPointagesJour.Clear();
        if (_periodeSelectionnee == null)
        {
            CellulesCalendrier.Clear();
            OnPropertyChanged(nameof(MoisCalendrierLibelle));
            OnPropertyChanged(nameof(TotalHeuresMoisAfficheLibelle));
            OnPropertyChanged(nameof(TotalJoursMoisAfficheLibelle));
            NotifyModeTableauBas();
            return;
        }

        _moisCalendrier = _periodeSelectionnee.Mois;
        _anneeCalendrier = _periodeSelectionnee.Annee;
        OnPropertyChanged(nameof(MoisCalendrierLibelle));
        OnPropertyChanged(nameof(TotalHeuresMoisAfficheLibelle));
        OnPropertyChanged(nameof(TotalJoursMoisAfficheLibelle));
        ConstruireGrilleCalendrier();
        NotifyModeTableauBas();
    }

    private void MoisPrecedent()
    {
        var d = new DateTime(_anneeCalendrier, _moisCalendrier, 1).AddMonths(-1);
        _moisCalendrier = d.Month;
        _anneeCalendrier = d.Year;
        _dateSelectionnee = null;
        DetailJour = null;
        LignesPointagesJour.Clear();
        OnPropertyChanged(nameof(MoisCalendrierLibelle));
        OnPropertyChanged(nameof(TotalHeuresMoisAfficheLibelle));
        OnPropertyChanged(nameof(TotalJoursMoisAfficheLibelle));
        ConstruireGrilleCalendrier();
        NotifyModeTableauBas();
    }

    private void MoisSuivant()
    {
        var d = new DateTime(_anneeCalendrier, _moisCalendrier, 1).AddMonths(1);
        _moisCalendrier = d.Month;
        _anneeCalendrier = d.Year;
        _dateSelectionnee = null;
        DetailJour = null;
        LignesPointagesJour.Clear();
        OnPropertyChanged(nameof(MoisCalendrierLibelle));
        OnPropertyChanged(nameof(TotalHeuresMoisAfficheLibelle));
        OnPropertyChanged(nameof(TotalJoursMoisAfficheLibelle));
        ConstruireGrilleCalendrier();
        NotifyModeTableauBas();
    }

    private void NotifyModeTableauBas()
    {
        OnPropertyChanged(nameof(AfficherTableauRecapEmployesPeriode));
        OnPropertyChanged(nameof(AfficherTableauPointagesJourSelectionne));
        OnPropertyChanged(nameof(TitreTableauBas));
        if (_enregistrerTypeJourCommand is RelayCommand rt)
            rt.RaiseCanExecuteChanged();
    }

    private void SelectionnerJour(object? p)
    {
        if (p is not CalendrierJourCellVm c || !c.EstDansMoisVisible || EmployeSelectionne == null)
            return;
        _dateSelectionnee = c.Date.Date;
        ConstruireGrilleCalendrier();
        RafraichirDetailJour();
    }

    private void RafraichirDetailJour(string? messageApres = null)
    {
        if (!_dateSelectionnee.HasValue || EmployeSelectionne == null)
        {
            DetailJour = null;
            LignesPointagesJour.Clear();
            NotifyModeTableauBas();
            return;
        }

        var d = _dateSelectionnee.Value.Date;
        var lignes = SuiviJournalierPdfDataService.ObtenirLignesPourEmploye(_db, EmployeSelectionne.Id, d.Month, d.Year);
        var debut = new DateTime(d.Year, d.Month, 1);
        var idx = (int)(d - debut).TotalDays;
        if (idx < 0 || idx >= lignes.Count)
        {
            DetailJour = null;
            LignesPointagesJour.Clear();
            NotifyModeTableauBas();
            return;
        }

        var ligne = lignes[idx];
        var suivi = _db.SuivisJournaliers.AsNoTracking()
            .FirstOrDefault(s => s.EmployeId == EmployeSelectionne.Id && s.Date.Date == d);
        var pts = PointagesJournalierSerializer.Deserialiser(suivi?.PointagesJson, d);

        var reglesPourDetail = LtServicesReglesProvider.ChargerDepuisDb(_db);
        var detail = DetailJourEmployeVm.Creer(d, ligne, suivi, pts, reglesPourDetail);
        DetailJour = detail;
        if (!string.IsNullOrWhiteSpace(messageApres))
            detail.DefinirMessageStatut(messageApres);

        var reglesLt = LtServicesReglesProvider.ChargerDepuisDb(_db);
        LignesPointagesJour.Clear();
        foreach (var row in LtServicesPointageCalcul.DecrirePointagesPourAffichage(pts, d, reglesLt))
            LignesPointagesJour.Add(row);
        NotifyModeTableauBas();
    }

    private void EnregistrerMoments()
    {
        if (DetailJour == null || EmployeSelectionne == null || !_dateSelectionnee.HasValue)
            return;
        if (!DetailJour.PeutEditerMoments)
            return;

        if (!DetailJour.TryConstruireListePourEnregistrement(out var liste, out var erreur))
        {
            DetailJour.DefinirMessageStatut(erreur);
            return;
        }

        var d = _dateSelectionnee.Value.Date;
        var sj = _db.SuivisJournaliers.FirstOrDefault(s => s.EmployeId == EmployeSelectionne.Id && s.Date.Date == d);

        if (sj != null && sj.HeuresManuelles)
        {
            DetailJour.DefinirMessageStatut("Heures saisies manuellement : utilisez le pointage journalier pour modifier.");
            return;
        }

        if (sj != null && sj.TypeJour != SuiviJournalier.TypeNormal)
        {
            DetailJour.DefinirMessageStatut("Seuls les jours de type « Normal » peuvent être modifiés ici.");
            return;
        }

        if (sj == null && liste.Count == 0)
        {
            DetailJour.DefinirMessageStatut("Saisissez au moins une heure.");
            return;
        }

        if (sj == null)
        {
            sj = new SuiviJournalier
            {
                EmployeId = EmployeSelectionne.Id,
                Date = d,
                TypeJour = SuiviJournalier.TypeNormal,
                HeuresManuelles = false
            };
            _db.SuivisJournaliers.Add(sj);
        }

        sj!.PointagesJson = liste.Count > 0 ? PointagesJournalierSerializer.Serialiser(liste) : null;
        sj.HeuresManuelles = false;
        var reglesLt = LtServicesReglesProvider.ChargerDepuisDb(_db);
        sj.HeuresPrestees = string.IsNullOrEmpty(sj.PointagesJson)
            ? 0m
            : PointagesJournalierSerializer.CalculerHeuresLt(sj.PointagesJson, d, reglesLt);

        _db.SaveChanges();
        RafraichirDetailJour("Enregistré.");
        ConstruireGrilleCalendrier();
        ChargerTotaux();
        OnPropertyChanged(nameof(TotalHeuresMoisAfficheLibelle));
        OnPropertyChanged(nameof(TotalJoursMoisAfficheLibelle));
        UiFeedback.Succes("Pointages du jour enregistrés.");
    }

    private void EnregistrerTypeJour()
    {
        if (DetailJour == null || EmployeSelectionne == null || !_dateSelectionnee.HasValue)
            return;

        var d = _dateSelectionnee.Value.Date;
        var typeJour = string.IsNullOrWhiteSpace(DetailJour.TypeJourSelectionne)
            ? SuiviJournalier.TypeNormal
            : DetailJour.TypeJourSelectionne.Trim();

        var sj = _db.SuivisJournaliers.FirstOrDefault(s => s.EmployeId == EmployeSelectionne.Id && s.Date.Date == d);
        if (sj == null)
        {
            sj = new SuiviJournalier
            {
                EmployeId = EmployeSelectionne.Id,
                Date = d
            };
            _db.SuivisJournaliers.Add(sj);
        }

        sj.TypeJour = typeJour;

        if (typeJour != SuiviJournalier.TypeNormal)
        {
            // Jour spécial : pas de pointages pris en compte.
            sj.PointagesJson = null;
            sj.HeuresManuelles = false;
            sj.HeuresPrestees =
                SuiviJournalier.EstTypeJourSpecialPaye(typeJour)
                    ? SuiviJournalierCalculPaieHelper.DeterminerHeuresNominalesJourDepuisDb(_db, d)
                    : 0m;
        }
        else if (!string.IsNullOrWhiteSpace(sj.PointagesJson))
        {
            var regles = LtServicesReglesProvider.ChargerDepuisDb(_db);
            sj.HeuresPrestees = PointagesJournalierSerializer.CalculerHeuresLt(sj.PointagesJson, d, regles);
            sj.HeuresManuelles = false;
        }
        else
        {
            // Normal = heures réelles uniquement, donc 0 si aucun pointage.
            sj.HeuresPrestees = 0m;
            sj.HeuresManuelles = false;
        }

        _db.SaveChanges();
        RafraichirDetailJour("Type de jour enregistré.");
        ConstruireGrilleCalendrier();
        ChargerTotaux();
        OnPropertyChanged(nameof(TotalHeuresMoisAfficheLibelle));
        OnPropertyChanged(nameof(TotalJoursMoisAfficheLibelle));
        UiFeedback.Succes("Type de jour enregistré.");
    }

    private void ConstruireGrilleCalendrier()
    {
        CellulesCalendrier.Clear();
        if (_periodeSelectionnee == null)
            return;

        IReadOnlyDictionary<DateTime, SuiviJournalierPdfLigne>? parDate = null;
        if (EmployeSelectionne != null)
            parDate = SuiviJournalierPdfDataService.ObtenirLignesParDate(_db, EmployeSelectionne.Id, _moisCalendrier, _anneeCalendrier);

        var first = new DateTime(_anneeCalendrier, _moisCalendrier, 1);
        var startOffset = ((int)first.DayOfWeek + 6) % 7;
        var gridStart = first.AddDays(-startOffset);
        var today = DateTime.Today;

        for (var i = 0; i < 42; i++)
        {
            var date = gridStart.AddDays(i);
            var dansMois = date.Month == _moisCalendrier && date.Year == _anneeCalendrier;
            decimal h = 0;
            var typeJour = "";
            if (dansMois && parDate != null && parDate.TryGetValue(date.Date, out var ligne))
            {
                h = ligne.HeuresPrestees;
                typeJour = ligne.TypeJour ?? "";
            }

            var niveau = !dansMois ? 0 : h <= 0 ? 0 : h < 4 ? 1 : h < 8 ? 2 : 3;
            var heuresTexte = !dansMois ? "" : h > 0 ? h.ToString("N1", CultureInfo.CurrentCulture) : "—";
            var (badgeTexte, badgeFond, badgeTexteCouleur) = ConstruireBadgeTypeJour(typeJour);

            CellulesCalendrier.Add(new CalendrierJourCellVm
            {
                Date = date.Date,
                NumeroJour = date.Day,
                EstDansMoisVisible = dansMois,
                EstAujourdhui = date.Date == today,
                EstWeekEnd = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
                HeuresCourtLibelle = heuresTexte,
                NiveauActivite = niveau,
                EstSelectionne = _dateSelectionnee.HasValue && date.Date == _dateSelectionnee.Value.Date,
                TypeJourBadge = badgeTexte,
                TypeJourBadgeCouleurFond = badgeFond,
                TypeJourBadgeCouleurTexte = badgeTexteCouleur
            });
        }

        OnPropertyChanged(nameof(TotalHeuresMoisAfficheLibelle));
        OnPropertyChanged(nameof(TotalJoursMoisAfficheLibelle));
    }

    private static (string Texte, string Fond, string TexteCouleur) ConstruireBadgeTypeJour(string? typeJour)
    {
        var type = (typeJour ?? "").Trim();
        if (string.IsNullOrWhiteSpace(type) || string.Equals(type, SuiviJournalier.TypeNormal, StringComparison.OrdinalIgnoreCase))
            return ("", "#E2E8F0", "#334155");

        if (string.Equals(type, SuiviJournalier.TypeAbsence, StringComparison.OrdinalIgnoreCase))
            return ("ABS", "#FEE2E2", "#B91C1C");
        if (string.Equals(type, SuiviJournalier.TypeMaladie, StringComparison.OrdinalIgnoreCase))
            return ("MAL", "#E0F2FE", "#0369A1");
        if (string.Equals(type, SuiviJournalier.TypeCongeAnnuel, StringComparison.OrdinalIgnoreCase))
            return ("ANNUEL", "#FEF3C7", "#92400E");
        if (string.Equals(type, SuiviJournalier.TypeCongeCirconstance, StringComparison.OrdinalIgnoreCase))
            return ("CONGE", "#DCFCE7", "#166534");
        if (string.Equals(type, SuiviJournalier.TypePreavis, StringComparison.OrdinalIgnoreCase))
            return ("PREAVIS", "#EDE9FE", "#5B21B6");

        return (type.ToUpperInvariant(), "#E2E8F0", "#334155");
    }

    private void ChargerTotaux()
    {
        Lignes.Clear();
        TotalGeneralHeures = 0;
        TotalGeneralJoursEquivalent = 0;

        if (PeriodeSelectionnee == null)
            return;

        var mois = PeriodeSelectionnee.Mois;
        var annee = PeriodeSelectionnee.Annee;
        var (politique, debut, fin) = PeriodePaieHelper.ResoudrePeriode(_db, PeriodeSelectionnee);

        var employes = ContexteEntrepriseService.EmployesEntrepriseCourante(_db)
            .AsNoTracking()
            .Include(e => e.Departement)
            .OrderBy(e => e.Matricule)
            .ToList();

        decimal totalHeures = 0;
        decimal totalJours = 0;
        foreach (var e in employes)
        {
            var totaux = SuiviJournalierCalculPaieHelper.CalculerTotauxPresenceEmploye(_db, e.Id, debut, fin, politique);
            totalHeures += totaux.TotalHeures;
            totalJours += totaux.JoursEquivalents;
            var nom = $"{e.Nom} {e.Postnom} {e.Prenom}".Trim();
            Lignes.Add(new HeuresTotauxEmployeRow
            {
                EmployeId = e.Id,
                Matricule = e.Matricule,
                NomComplet = nom,
                Departement = e.Departement?.NomDepartement,
                TotalHeures = totaux.TotalHeures,
                TotalJoursEquivalent = totaux.JoursEquivalents
            });
        }

        TotalGeneralHeures = totalHeures;
        TotalGeneralJoursEquivalent = decimal.Round(totalJours, 2, MidpointRounding.AwayFromZero);
        (ExporterHeuresPeriodeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        ChargerSituationPaie();
    }

    private void ChargerSituationPaie()
    {
        LignesSituationPaie.Clear();
        if (PeriodeSelectionnee == null)
        {
            OnPropertyChanged(nameof(AfficherSectionRapports));
            (ExporterRapportQuinzainesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ExporterRapportMensuelCommand as RelayCommand)?.RaiseCanExecuteChanged();
            return;
        }

        foreach (var l in _rapportPaieService.ConstruireSituationPeriode(_db, PeriodeSelectionnee.Id))
            LignesSituationPaie.Add(l);

        OnPropertyChanged(nameof(AfficherSectionRapports));
        (ExporterRapportQuinzainesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExporterRapportMensuelCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public SituationPaieAgentLigne? SituationEmployeSelectionne =>
        EmployeSelectionne == null
            ? null
            : LignesSituationPaie.FirstOrDefault(l => l.EmployeId == EmployeSelectionne.Id);

    public void ExporterRapportAgentPdf(string cheminFichier)
    {
        if (PeriodeSelectionnee == null || EmployeSelectionne == null)
            throw new InvalidOperationException("Sélectionnez une période et un employé.");

        var ligne = SituationEmployeSelectionne
                    ?? _rapportPaieService.ConstruireSituationPeriode(_db, PeriodeSelectionnee.Id)
                        .FirstOrDefault(l => l.EmployeId == EmployeSelectionne.Id)
                    ?? throw new InvalidOperationException("Employé introuvable.");

        new ExportPdfService().ExporterRapportAgentSituationPdf(
            ligne, PeriodeSelectionnee.Mois, PeriodeSelectionnee.Annee, cheminFichier);
    }

    public void ExporterRapportQuinzainesPdf(string cheminFichier)
    {
        if (PeriodeSelectionnee == null || LignesSituationPaie.Count == 0)
            throw new InvalidOperationException("Aucune donnée pour la période.");

        new ExportPdfService().ExporterRapportQuinzainesPdf(
            LignesSituationPaie.ToList(), PeriodeSelectionnee.Mois, PeriodeSelectionnee.Annee, cheminFichier);
    }

    public void ExporterRapportMensuelSalairesPdf(string cheminFichier)
    {
        if (PeriodeSelectionnee == null || LignesSituationPaie.Count == 0)
            throw new InvalidOperationException("Aucune donnée pour la période.");

        new ExportPdfService().ExporterRapportMensuelSalairesPdf(
            LignesSituationPaie.ToList(), PeriodeSelectionnee.Mois, PeriodeSelectionnee.Annee, cheminFichier);
    }

    public void ExporterHeuresPeriodePdf(string cheminFichier)
    {
        if (PeriodeSelectionnee == null || Lignes.Count == 0)
            throw new InvalidOperationException("Aucune donnée d'heures pour la période.");

        var pdfLignes = Lignes.Select(l => new HeuresTotauxEmployePdfLigne(
            l.Matricule, l.NomComplet, l.Departement, l.TotalHeures, l.TotalJoursEquivalent)).ToList();

        new ExportPdfService().ExporterTotauxHeuresEmployesPdf(
            pdfLignes,
            PeriodeSelectionnee.Mois,
            PeriodeSelectionnee.Annee,
            TotalGeneralHeures,
            TotalGeneralJoursEquivalent,
            cheminFichier);
    }

    public void ExporterHeuresEmployePdf(string cheminFichier)
    {
        if (PeriodeSelectionnee == null || EmployeSelectionne == null)
            throw new InvalidOperationException("Sélectionnez une période et un employé.");

        var mois = PeriodeSelectionnee.Mois;
        var annee = PeriodeSelectionnee.Annee;
        var e = EmployeSelectionne;
        var nom = $"{e.Nom} {e.Postnom} {e.Prenom}".Trim();
        var lignes = SuiviJournalierPdfDataService.ObtenirLignesPourEmploye(_db, e.Id, mois, annee);

        new ExportPdfService().ExporterSuiviJournalierPdf(
            e.Matricule, nom, e.Departement?.NomDepartement, mois, annee, lignes, cheminFichier);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
