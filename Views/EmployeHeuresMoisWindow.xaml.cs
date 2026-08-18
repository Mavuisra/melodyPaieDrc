using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Views;

public partial class EmployeHeuresMoisWindow : Window
{
    private readonly int _employeId;
    private readonly PaieDbContext _db = new();
    private readonly List<PeriodeOption> _periodes = new();
    private readonly ObservableCollection<HeuresMoisLigne> _lignes = new();

    public EmployeHeuresMoisWindow(int employeId)
    {
        _employeId = employeId;
        InitializeComponent();
        var lectureSeule = !DroitsUi.PeutModifier;
        HeuresDataGrid.IsReadOnly = lectureSeule;
        BtnEnregistrer.IsEnabled = !lectureSeule;
        ChargerEmploye();
        ChargerPeriodes();
    }

    private void ChargerEmploye()
    {
        var emp = _db.Employes
            .AsNoTracking()
            .Include(e => e.Departement)
            .FirstOrDefault(e => e.Id == _employeId);
        if (emp == null)
        {
            UiFeedback.Avertissement("Employé introuvable.");
            Close();
            return;
        }

        var nom = $"{emp.Nom} {emp.Postnom} {emp.Prenom}".Trim();
        EmployeNomText.Text = string.IsNullOrWhiteSpace(nom) ? "Employé" : nom;
        EmployeMetaText.Text = $"Matricule: {emp.Matricule}  |  Département: {emp.Departement?.NomDepartement ?? "—"}";
    }

    private void ChargerPeriodes()
    {
        _periodes.Clear();
        foreach (var p in _db.PeriodesPaie.AsNoTracking().OrderByDescending(p => p.Annee).ThenByDescending(p => p.Mois))
        {
            _periodes.Add(new PeriodeOption(p.Mois, p.Annee));
        }

        if (_periodes.Count == 0)
        {
            var now = DateTime.Today;
            _periodes.Add(new PeriodeOption(now.Month, now.Year));
        }

        PeriodeCombo.ItemsSource = _periodes;
        PeriodeCombo.SelectedIndex = 0;
    }

    private void ChargerHeures(PeriodeOption periode)
    {
        var periodePaie = new PeriodePaie { Mois = periode.Mois, Annee = periode.Annee };
        var (politique, dateDebut, dateFin) = PeriodePaieHelper.ResoudrePeriode(_db, periodePaie);
        var reglesLt = LtServicesReglesProvider.ChargerDepuisDb(_db);

        var existantsList = _db.SuivisJournaliers
            .AsNoTracking()
            .Where(s => s.EmployeId == _employeId && s.Date >= dateDebut && s.Date <= dateFin)
            .ToList();
        var existants = existantsList.ToDictionary(s => s.Date.Date);

        var calendrierCtx = SuiviJournalierCalculPaieHelper.ChargerCalendrierPaie(_db, dateDebut, dateFin);
        var semaineSixJours = calendrierCtx.SemaineSixJours || politique.ForcerSamediOuvre;
        var fusionnes = SuiviJournalierGrilleHelper.FusionnerMoisCompletPourCalculPaie(
            _employeId,
            dateDebut,
            dateFin,
            existantsList,
            semaineSixJours,
            calendrierCtx.Calendrier,
            politique.CompleterJoursSansSaisie,
            politique.ForcerSamediOuvre);

        _lignes.Clear();
        foreach (var s in fusionnes)
        {
            existants.TryGetValue(s.Date.Date, out var row);
            var typeJour = string.IsNullOrWhiteSpace(row?.TypeJour) ? s.TypeJour : row!.TypeJour.Trim();
            decimal heures;
            var manuel = row?.HeuresManuelles ?? false;
            if (row != null && typeJour == SuiviJournalier.TypeNormal && !string.IsNullOrEmpty(row.PointagesJson) && !row.HeuresManuelles)
            {
                heures = PointagesJournalierSerializer.CalculerHeuresLt(row.PointagesJson, s.Date, reglesLt);
            }
            else if (row != null)
            {
                heures = row.HeuresPrestees;
            }
            else
            {
                heures = s.HeuresPrestees;
            }

            var ligne = new HeuresMoisLigne
            {
                Date = s.Date,
                TypeJour = typeJour,
                HeuresPrestees = decimal.Round(Math.Max(0m, Math.Min(24m, heures)), 2, MidpointRounding.AwayFromZero),
                PointagesJson = row?.PointagesJson,
                HeuresManuelles = manuel
            };
            ligne.PropertyChanged += (_, _) => RecalculerTotal();
            _lignes.Add(ligne);
        }

        HeuresDataGrid.ItemsSource = _lignes;
        RecalculerTotal();
    }

    private void PeriodeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (PeriodeCombo.SelectedItem is not PeriodeOption periode)
            return;
        ChargerHeures(periode);
    }

    private void Enregistrer_Click(object sender, RoutedEventArgs e)
    {
        if (!DroitsUi.PeutModifier)
            return;

        if (PeriodeCombo.SelectedItem is not PeriodeOption periode)
            return;

        try
        {
            var periodePaie = new PeriodePaie { Mois = periode.Mois, Annee = periode.Annee };
            var (_, dateDebut, dateFin) = PeriodePaieHelper.ResoudrePeriode(_db, periodePaie);
            var existants = _db.SuivisJournaliers
                .Where(s => s.EmployeId == _employeId && s.Date >= dateDebut && s.Date <= dateFin)
                .ToDictionary(s => s.Date.Date);

            foreach (var ligne in _lignes)
            {
                var heures = decimal.Round(Math.Max(0m, Math.Min(24m, ligne.HeuresPrestees)), 2, MidpointRounding.AwayFromZero);
                var typeJour = string.IsNullOrWhiteSpace(ligne.TypeJour) ? SuiviJournalier.TypeNormal : ligne.TypeJour.Trim();

                if (existants.TryGetValue(ligne.Date.Date, out var s))
                {
                    s.HeuresPrestees = heures;
                    s.TypeJour = typeJour;
                    s.HeuresManuelles = true;
                    // Conserver les horodatages déjà saisis / importés.
                }
                else
                {
                    _db.SuivisJournaliers.Add(new SuiviJournalier
                    {
                        EmployeId = _employeId,
                        Date = ligne.Date.Date,
                        HeuresPrestees = heures,
                        TypeJour = typeJour,
                        PointagesJson = ligne.PointagesJson,
                        HeuresManuelles = true
                    });
                }
            }

            _db.SaveChanges();
            AppSessionEvents.NotifierDonneesMetierModifiees();
            UiFeedback.Succes("Heures du mois enregistrées et synchronisées avec l’historique.");
            ChargerHeures(periode);
        }
        catch (Exception ex)
        {
            UiFeedback.Avertissement(ex.Message);
        }
    }

    private void Fermer_Click(object sender, RoutedEventArgs e) => Close();

    private void RecalculerTotal()
        => TotalHeuresText.Text = _lignes.Sum(l => l.HeuresPrestees).ToString("N2", CultureInfo.InvariantCulture);

    private sealed class HeuresMoisLigne : INotifyPropertyChanged
    {
        private decimal _heuresPrestees;
        private string _typeJour = SuiviJournalier.TypeNormal;
        private bool _heuresManuelles;
        public DateTime Date { get; set; }
        public string? PointagesJson { get; set; }

        public string DateAffichage => Date.ToString("dd/MM/yyyy");
        public string JourSemaine => Date.ToString("dddd", new CultureInfo("fr-FR"));

        public decimal HeuresPrestees
        {
            get => _heuresPrestees;
            set
            {
                var v = decimal.Round(Math.Max(0m, Math.Min(24m, value)), 2, MidpointRounding.AwayFromZero);
                if (_heuresPrestees == v) return;
                _heuresPrestees = v;
                HeuresManuelles = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(JourCode));
            }
        }

        public string TypeJour
        {
            get => _typeJour;
            set
            {
                var v = string.IsNullOrWhiteSpace(value) ? SuiviJournalier.TypeNormal : value.Trim();
                if (_typeJour == v) return;
                _typeJour = v;
                HeuresManuelles = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(JourCode));
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
                OnPropertyChanged(nameof(ModeCalcul));
            }
        }

        public int JourCode => TypeJour == SuiviJournalier.TypeNormal && HeuresPrestees > 0m ? 1 : 0;
        public string ModeCalcul => HeuresManuelles ? "Manuel" : (!string.IsNullOrEmpty(PointagesJson) ? "Auto (LT)" : "—");

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private sealed record PeriodeOption(int Mois, int Annee)
    {
        public string Libelle => $"{Mois:D2}/{Annee}";
    }
}
