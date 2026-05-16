using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.ViewModels;

/// <summary>Ligne du tableau des totaux d’heures pour un employé sur une période.</summary>
public sealed class HeuresTotauxEmployeRow
{
    private const decimal HeuresParJourEquivalent = 8m;
    public string Matricule { get; init; } = "";
    public string NomComplet { get; init; } = "";
    public string? Departement { get; init; }
    public decimal TotalHeures { get; init; }

    public string TotalHeuresLibelle =>
        TotalHeures.ToString("N2", CultureInfo.CurrentCulture) + " h";

    public decimal TotalJoursEquivalent =>
        HeuresParJourEquivalent <= 0m
            ? 0m
            : decimal.Round(TotalHeures / HeuresParJourEquivalent, 2, MidpointRounding.AwayFromZero);

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
    private const decimal HeuresParJourEquivalent = 8m;

    private readonly PaieDbContext _db;
    private PeriodePaie? _periodeSelectionnee;
    private decimal _totalGeneralHeures;
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

    public HeuresPresteesTotauxViewModel(PaieDbContext db)
    {
        _db = db;
        PeriodesPaie = new ObservableCollection<PeriodePaie>();
        Lignes = new ObservableCollection<HeuresTotauxEmployeRow>();
        Employes = new ObservableCollection<Employe>();
        CellulesCalendrier = new ObservableCollection<CalendrierJourCellVm>();
        LignesPointagesJour = new ObservableCollection<PointageAffichageLtDto>();
        ChargerPeriodes();
        ChargerEmployes();
    }

    public ObservableCollection<PeriodePaie> PeriodesPaie { get; }

    public ObservableCollection<HeuresTotauxEmployeRow> Lignes { get; }

    public ObservableCollection<Employe> Employes { get; }

    public ObservableCollection<CalendrierJourCellVm> CellulesCalendrier { get; }

    /// <summary>Lecture seule : une ligne par horodatage du jour sélectionné (employé + date).</summary>
    public ObservableCollection<PointageAffichageLtDto> LignesPointagesJour { get; }

    /// <summary>Faux tant qu’il n’y a pas de période exploitable : afficher le message d’aide à la place du tableau.</summary>
    public bool AfficherTableau => PeriodesPaie.Count > 0 && PeriodeSelectionnee != null;

    /// <summary>Vue calendrier et détail jour lorsqu’un employé est choisi.</summary>
    public bool AfficherCalendrierEmploye => AfficherTableau && EmployeSelectionne != null;

    /// <summary>Tableau récap tous employés (période) — masqué lorsqu’un jour est sélectionné dans le calendrier.</summary>
    public bool AfficherTableauRecapEmployesPeriode =>
        AfficherTableau && (EmployeSelectionne == null || !_dateSelectionnee.HasValue);

    /// <summary>Tableau des pointages uniquement pour le jour sélectionné (employé obligatoire).</summary>
    public bool AfficherTableauPointagesJourSelectionne =>
        AfficherTableau && EmployeSelectionne != null && _dateSelectionnee.HasValue;

    public string TitreTableauBas
    {
        get
        {
            if (!AfficherTableau)
                return "";
            if (EmployeSelectionne != null && _dateSelectionnee.HasValue)
            {
                var e = EmployeSelectionne;
                var nom = $"{e.Nom} {e.Postnom} {e.Prenom}".Trim();
                var d = _dateSelectionnee.Value;
                return $"Pointages du {d.ToString("dddd d MMMM yyyy", Fr)} — {e.Matricule} · {nom}";
            }

            return "Totaux d’heures par employé (période de paie)";
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
            var t = SuiviJournalierPdfDataService.CalculerTotalHeuresPourEmploye(_db, EmployeSelectionne.Id, _moisCalendrier, _anneeCalendrier);
            var j = HeuresParJourEquivalent <= 0m ? 0m : decimal.Round(t / HeuresParJourEquivalent, 2, MidpointRounding.AwayFromZero);
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

    public decimal TotalGeneralJoursEquivalent =>
        HeuresParJourEquivalent <= 0m
            ? 0m
            : decimal.Round(TotalGeneralHeures / HeuresParJourEquivalent, 2, MidpointRounding.AwayFromZero);

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
            _ => DetailJour?.PeutEditerMoments == true);

    public ICommand EnregistrerTypeJourCommand =>
        _enregistrerTypeJourCommand ??= new RelayCommand(
            _ => EnregistrerTypeJour(),
            _ => DetailJour != null && EmployeSelectionne != null && _dateSelectionnee.HasValue);

    public ICommand ActualiserTotauxCommand =>
        _actualiserTotauxCommand ??= new RelayCommand(_ => RafraichirTotaux());

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

    private void ChargerEmployes()
    {
        var idConserve = EmployeSelectionne?.Id;
        Employes.Clear();
        foreach (var e in _db.Employes.AsNoTracking().Include(x => x.Departement).OrderBy(x => x.Matricule))
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

        var detail = DetailJourEmployeVm.Creer(d, ligne, suivi, pts);
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
                string.Equals(typeJour, SuiviJournalier.TypeMaladie, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(typeJour, SuiviJournalier.TypeCongeCirconstance, StringComparison.OrdinalIgnoreCase)
                    ? DeterminerHeuresNominalesJour(d)
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
    }

    private decimal DeterminerHeuresNominalesJour(DateTime d)
    {
        var date = d.Date;
        var calendrier = _db.JoursTravailCalendrier
            .AsNoTracking()
            .Where(j => j.DateJour.Date == date)
            .FirstOrDefault();

        if (calendrier != null)
        {
            if (string.Equals(calendrier.TypeJour, "Repos", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(calendrier.TypeJour, "Ferie", StringComparison.OrdinalIgnoreCase))
                return 0m;

            if (string.Equals(calendrier.TypeJour, "Ouvre", StringComparison.OrdinalIgnoreCase))
                return date.DayOfWeek == DayOfWeek.Saturday
                    ? LtServicesPointageCalcul.HeuresNormalesSamedi
                    : LtServicesPointageCalcul.HeuresNormalesJourSemaine;
        }

        if (date.DayOfWeek == DayOfWeek.Sunday)
            return 0m;
        if (date.DayOfWeek == DayOfWeek.Saturday)
            return 0m;
        return LtServicesPointageCalcul.HeuresNormalesJourSemaine;
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

        if (PeriodeSelectionnee == null)
            return;

        var mois = PeriodeSelectionnee.Mois;
        var annee = PeriodeSelectionnee.Annee;

        var employes = _db.Employes
            .AsNoTracking()
            .Include(e => e.Departement)
            .OrderBy(e => e.Matricule)
            .ToList();

        decimal total = 0;
        foreach (var e in employes)
        {
            var h = SuiviJournalierPdfDataService.CalculerTotalHeuresPourEmploye(_db, e.Id, mois, annee);
            total += h;
            var nom = $"{e.Nom} {e.Postnom} {e.Prenom}".Trim();
            Lignes.Add(new HeuresTotauxEmployeRow
            {
                Matricule = e.Matricule,
                NomComplet = nom,
                Departement = e.Departement?.NomDepartement,
                TotalHeures = h
            });
        }

        TotalGeneralHeures = total;
        OnPropertyChanged(nameof(TotalGeneralJoursEquivalent));
        OnPropertyChanged(nameof(TotalGeneralJoursEquivalentLibelle));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
