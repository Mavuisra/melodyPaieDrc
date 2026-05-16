using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.ViewModels;

public class ContratViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private readonly int _employeId;
    private Contrat? _selectionne;

    public ContratViewModel(PaieDbContext db, int employeId)
    {
        _db = db;
        _employeId = employeId;
        Contrats = new ObservableCollection<Contrat>();
        Categories = new ObservableCollection<CategorieProfessionnelle>();
        TypesContrat = new ObservableCollection<string> { "CDI", "CDD", "Stage", "Journalier" };
        Devises = new ObservableCollection<string> { "USD", "CDF" };

        NouveauContrat = new Contrat
        {
            EmployeId = employeId,
            TypeContrat = "CDI",
            DateDebut = DateTime.Today,
            SalaireBase = 0,
            DeviseBase = "USD",
            // Valeurs par défaut (peuvent être ajustées par l'utilisateur)
            TauxMajorationHeuresSup = 50m,
            TauxMajorationNuit = 30m,
            TauxMajorationJourFerie = 100m,
            PreavisMoisBase = 1m,
            IndemniteLicenciementMoisBase = 0m
        };

        AjouterCommand = new RelayCommand(_ => Ajouter());
        SupprimerCommand = new RelayCommand(_ => Supprimer(), _ => Selectionne != null && !EmployeDejaPaye);
    }

    public string NomEmploye { get; set; } = "";

    /// <summary>True si l'employé a déjà été payé au moins une fois (suppression contrat désactivée).</summary>
    public bool EmployeDejaPaye => _db.BulletinsPaie.Any(b => b.EmployeId == _employeId);

    public ObservableCollection<Contrat> Contrats { get; }
    public ObservableCollection<CategorieProfessionnelle> Categories { get; }
    public ObservableCollection<string> TypesContrat { get; }
    public ObservableCollection<string> Devises { get; }

    public Contrat NouveauContrat { get; }

    public Contrat? Selectionne
    {
        get => _selectionne;
        set { _selectionne = value; OnPropertyChanged(); OnPropertyChanged(nameof(EmployeDejaPaye)); (SupprimerCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    public ICommand AjouterCommand { get; }
    public ICommand SupprimerCommand { get; }

    public Action<string>? OnErreur { get; set; }

    public void Charger()
    {
        var employe = _db.Employes.Find(_employeId);
        NomEmploye = employe != null ? $"{employe.Nom} {employe.Prenom}".Trim() : "";

        Categories.Clear();
        foreach (var c in _db.CategoriesProfessionnelles.OrderBy(x => x.Libelle))
            Categories.Add(c);

        if (NouveauContrat.CategorieProfessionnelleId <= 0 && Categories.Count > 0)
            NouveauContrat.CategorieProfessionnelleId = Categories[0].Id;

        Contrats.Clear();
        foreach (var c in _db.Contrats
            .Include(x => x.CategorieProfessionnelle)
            .Where(x => x.EmployeId == _employeId)
            .OrderByDescending(x => x.DateDebut))
        {
            Contrats.Add(c);
        }
        OnPropertyChanged(nameof(EmployeDejaPaye));
        (SupprimerCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void Ajouter()
    {
        if (string.IsNullOrWhiteSpace(NouveauContrat.TypeContrat))
        { OnErreur?.Invoke("Sélectionnez un type de contrat."); return; }
        if (NouveauContrat.SalaireBase <= 0)
        { OnErreur?.Invoke("Le salaire de base doit être supérieur à 0."); return; }
        if (NouveauContrat.CategorieProfessionnelleId <= 0)
        { OnErreur?.Invoke("Sélectionnez une catégorie professionnelle."); return; }
        if (string.Equals(NouveauContrat.TypeContrat, "CDI", StringComparison.OrdinalIgnoreCase) && NouveauContrat.DateFin.HasValue)
        { OnErreur?.Invoke("Un contrat CDI ne peut pas avoir de date de fin."); return; }
        if (!string.Equals(NouveauContrat.TypeContrat, "CDI", StringComparison.OrdinalIgnoreCase) && !NouveauContrat.DateFin.HasValue)
        { OnErreur?.Invoke("Une date de fin est obligatoire pour un contrat CDD, Stage ou Journalier."); return; }
        if (NouveauContrat.DateFin.HasValue && NouveauContrat.DateFin.Value.Date < NouveauContrat.DateDebut.Date)
        { OnErreur?.Invoke("La date de fin doit être postérieure ou égale à la date de début."); return; }
        if (Contrats.Count > 0)
        { OnErreur?.Invoke("Un employé ne peut avoir qu'un seul contrat. Terminez ou supprimez le contrat existant avant d'en ajouter un nouveau."); return; }

        try
        {
            _db.Contrats.Add(new Contrat
            {
                EmployeId = _employeId,
                TypeContrat = NouveauContrat.TypeContrat,
                DateDebut = NouveauContrat.DateDebut,
                DateFin = NouveauContrat.DateFin,
                SalaireBase = NouveauContrat.SalaireBase,
                DeviseBase = NouveauContrat.DeviseBase ?? "USD",
                CategorieProfessionnelleId = NouveauContrat.CategorieProfessionnelleId,
                TauxMajorationHeuresSup = NouveauContrat.TauxMajorationHeuresSup,
                TauxMajorationNuit = NouveauContrat.TauxMajorationNuit,
                TauxMajorationJourFerie = NouveauContrat.TauxMajorationJourFerie,
                PreavisMoisBase = NouveauContrat.PreavisMoisBase,
                IndemniteLicenciementMoisBase = NouveauContrat.IndemniteLicenciementMoisBase
            });
            _db.SaveChanges();
            Charger();
            // Réinitialiser le formulaire pour un autre ajout
            NouveauContrat.TypeContrat = "CDI";
            NouveauContrat.DateDebut = DateTime.Today;
            NouveauContrat.DateFin = null;
            NouveauContrat.SalaireBase = 0;
            NouveauContrat.DeviseBase = "USD";
            OnPropertyChanged(nameof(NouveauContrat));
        }
        catch (Exception ex) { OnErreur?.Invoke(ex.Message); }
    }

    private void Supprimer()
    {
        if (Selectionne is null) return;
        try
        {
            var entite = _db.Contrats.Find(Selectionne.Id);
            if (entite != null)
            {
                _db.Contrats.Remove(entite);
                _db.SaveChanges();
                Charger();
            }
        }
        catch (Exception ex) { OnErreur?.Invoke(ex.Message); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
