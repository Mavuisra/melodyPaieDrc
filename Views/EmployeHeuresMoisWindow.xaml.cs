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
            MessageBox.Show(this, "Employé introuvable.", "Heures de travail", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        var dateDebut = new DateTime(periode.Annee, periode.Mois, 1);
        var dateFin = dateDebut.AddMonths(1).AddDays(-1);
        var reglesLt = LtServicesReglesProvider.ChargerDepuisDb(_db);

        var existants = _db.SuivisJournaliers
            .AsNoTracking()
            .Where(s => s.EmployeId == _employeId && s.Date >= dateDebut && s.Date <= dateFin)
            .ToDictionary(s => s.Date.Date);

        _lignes.Clear();
        for (var d = dateDebut.Date; d <= dateFin.Date; d = d.AddDays(1))
        {
            existants.TryGetValue(d, out var s);
            var typeJour = string.IsNullOrWhiteSpace(s?.TypeJour) ? SuiviJournalier.TypeNormal : s!.TypeJour.Trim();
            decimal heures;
            var manuel = s?.HeuresManuelles ?? false;
            if (s != null && typeJour == SuiviJournalier.TypeNormal && !string.IsNullOrEmpty(s.PointagesJson) && !s.HeuresManuelles)
            {
                heures = PointagesJournalierSerializer.CalculerHeuresLt(s.PointagesJson, d, reglesLt);
            }
            else
            {
                heures = s?.HeuresPrestees ?? 0m;
            }

            var ligne = new HeuresMoisLigne
            {
                Date = d,
                TypeJour = typeJour,
                HeuresPrestees = decimal.Round(Math.Max(0m, Math.Min(24m, heures)), 2, MidpointRounding.AwayFromZero),
                PointagesJson = s?.PointagesJson,
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
        if (PeriodeCombo.SelectedItem is not PeriodeOption periode)
            return;

        try
        {
            var dateDebut = new DateTime(periode.Annee, periode.Mois, 1).Date;
            var dateFin = dateDebut.AddMonths(1).AddDays(-1).Date;
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
                }
                else
                {
                    _db.SuivisJournaliers.Add(new SuiviJournalier
                    {
                        EmployeId = _employeId,
                        Date = ligne.Date.Date,
                        HeuresPrestees = heures,
                        TypeJour = typeJour,
                        PointagesJson = null,
                        HeuresManuelles = true
                    });
                }
            }

            _db.SaveChanges();
            MessageBox.Show(this, "Les heures du mois ont été enregistrées.", "Heures de travail",
                MessageBoxButton.OK, MessageBoxImage.Information);
            ChargerHeures(periode);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Heures de travail", MessageBoxButton.OK, MessageBoxImage.Warning);
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
