using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.ViewModels;

/// <summary>Rapports paie par agent dans le module Employés.</summary>
public sealed class EmployeRapportViewModel : INotifyPropertyChanged
{
    private static readonly CultureInfo Fr = new("fr-FR");

    private readonly PaieDbContext _db;
    private readonly HeuresPaieRapportService _rapportService = new();

    private PeriodePaie? _periodeSelectionnee;
    private Employe? _employeSelectionne;
    private SituationPaieAgentLigne? _ligneSituationSelectionnee;

    private ICommand? _actualiserCommand;
    private ICommand? _exporterRapportAgentCommand;
    private ICommand? _exporterRapportQuinzainesCommand;
    private ICommand? _exporterRapportMensuelCommand;

    public EmployeRapportViewModel(PaieDbContext db)
    {
        _db = db;
        PeriodesPaie = new ObservableCollection<PeriodePaie>();
        LignesSituationPaie = new ObservableCollection<SituationPaieAgentLigne>();
        Employes = new ObservableCollection<Employe>();
        ChargerPeriodes();
        ChargerEmployes();
    }

    public ObservableCollection<PeriodePaie> PeriodesPaie { get; }

    public ObservableCollection<SituationPaieAgentLigne> LignesSituationPaie { get; }

    public ObservableCollection<Employe> Employes { get; }

    public PeriodePaie? PeriodeSelectionnee
    {
        get => _periodeSelectionnee;
        set
        {
            _periodeSelectionnee = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PeriodeLibelle));
            OnPropertyChanged(nameof(MessageVide));
            OnPropertyChanged(nameof(AfficherSituation));
            ChargerSituationPaie();
        }
    }

    public SituationPaieAgentLigne? LigneSituationSelectionnee
    {
        get => _ligneSituationSelectionnee;
        set
        {
            if (_ligneSituationSelectionnee == value) return;
            _ligneSituationSelectionnee = value;
            OnPropertyChanged();
            if (value != null && _employeSelectionne?.Id != value.EmployeId)
                EmployeSelectionne = Employes.FirstOrDefault(e => e.Id == value.EmployeId);
        }
    }

    public Employe? EmployeSelectionne
    {
        get => _employeSelectionne;
        set
        {
            if (_employeSelectionne == value) return;
            _employeSelectionne = value;
            _ligneSituationSelectionnee = value == null
                ? null
                : LignesSituationPaie.FirstOrDefault(l => l.EmployeId == value.Id);
            OnPropertyChanged();
            OnPropertyChanged(nameof(LigneSituationSelectionnee));
            OnPropertyChanged(nameof(SituationEmployeSelectionne));
            OnPropertyChanged(nameof(AfficherDetailEmploye));
            (ExporterRapportAgentCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string PeriodeLibelle =>
        PeriodeSelectionnee == null
            ? "—"
            : $"{Fr.DateTimeFormat.GetMonthName(PeriodeSelectionnee.Mois)} {PeriodeSelectionnee.Annee}";

    public bool AfficherSituation => PeriodeSelectionnee != null && LignesSituationPaie.Count > 0;

    public bool AfficherDetailEmploye => EmployeSelectionne != null && SituationEmployeSelectionne != null;

    public string MessageVide =>
        PeriodesPaie.Count == 0
            ? "Créez d'abord des périodes de paie dans Paramètres → Périodes de paie."
            : PeriodeSelectionnee == null
                ? "Sélectionnez une période de paie."
                : LignesSituationPaie.Count == 0
                    ? "Aucun employé pour cette entreprise."
                    : "";

    public SituationPaieAgentLigne? SituationEmployeSelectionne =>
        EmployeSelectionne == null
            ? null
            : LignesSituationPaie.FirstOrDefault(l => l.EmployeId == EmployeSelectionne.Id);

    public ICommand ActualiserCommand =>
        _actualiserCommand ??= new RelayCommand(_ => Rafraichir());

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

    public event Action? OnDemandeExportRapportAgent;
    public event Action? OnDemandeExportRapportQuinzaines;
    public event Action? OnDemandeExportRapportMensuel;

    public void Rafraichir()
    {
        ChargerEmployes();
        ChargerPeriodes();
    }

    public void RechargerPourEntrepriseCourante() => Rafraichir();

    public void SynchroniserEmployeDepuisRepertoire(int? employeId)
    {
        if (!employeId.HasValue)
        {
            EmployeSelectionne = null;
            return;
        }

        EmployeSelectionne = Employes.FirstOrDefault(e => e.Id == employeId.Value);
    }

    private void ChargerPeriodes()
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

        PeriodeSelectionnee = selectedId.HasValue
            ? PeriodesPaie.FirstOrDefault(x => x.Id == selectedId.Value) ?? PeriodesPaie[0]
            : PeriodesPaie[0];
    }

    private void ChargerEmployes()
    {
        var idConserve = EmployeSelectionne?.Id;
        Employes.Clear();
        foreach (var e in ContexteEntrepriseService.EmployesEntrepriseCourante(_db)
                     .AsNoTracking()
                     .Include(x => x.Departement)
                     .OrderBy(x => x.Matricule))
            Employes.Add(e);

        if (idConserve.HasValue)
            EmployeSelectionne = Employes.FirstOrDefault(x => x.Id == idConserve.Value);
    }

    private void ChargerSituationPaie()
    {
        LignesSituationPaie.Clear();
        if (PeriodeSelectionnee == null)
        {
            NotifierEtatRapports();
            return;
        }

        foreach (var l in _rapportService.ConstruireSituationPeriode(_db, PeriodeSelectionnee.Id))
            LignesSituationPaie.Add(l);

        if (_employeSelectionne != null)
        {
            _ligneSituationSelectionnee = LignesSituationPaie.FirstOrDefault(l => l.EmployeId == _employeSelectionne.Id);
            OnPropertyChanged(nameof(LigneSituationSelectionnee));
        }

        OnPropertyChanged(nameof(SituationEmployeSelectionne));
        OnPropertyChanged(nameof(AfficherDetailEmploye));
        NotifierEtatRapports();
    }

    private void NotifierEtatRapports()
    {
        OnPropertyChanged(nameof(AfficherSituation));
        OnPropertyChanged(nameof(MessageVide));
        (ExporterRapportQuinzainesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExporterRapportMensuelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExporterRapportAgentCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void ExporterRapportAgentPdf(string cheminFichier)
    {
        if (PeriodeSelectionnee == null || EmployeSelectionne == null)
            throw new InvalidOperationException("Sélectionnez une période et un employé.");

        var ligne = SituationEmployeSelectionne
                    ?? _rapportService.ConstruireSituationPeriode(_db, PeriodeSelectionnee.Id)
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
