using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
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

        AjouterCommand = new RelayCommand(_ => Ajouter(), _ => DroitsUi.PeutModifier);
        ModifierCommand = new RelayCommand(_ => Modifier(), _ => DroitsUi.PeutModifier && Selectionne != null);
        SupprimerCommand = new RelayCommand(_ => Supprimer(), _ => DroitsUi.PeutModifier && Selectionne != null && !EmployeDejaPaye);
        ExporterPdfCommand = new RelayCommand(_ => ExporterPdf(), _ => Selectionne != null);
    }

    public bool PeutModifier => DroitsUi.PeutModifier;

    public string NomEmploye { get; set; } = "";

    public decimal JoursReferencePaie { get; private set; } = SalaireReferenceHelper.JoursDefaut;

    public decimal HeuresParJour { get; private set; } = SalaireReferenceHelper.HeuresDefaut;

    public string SalaireJourEntete => $"Jour (/{JoursReferencePaie:0.##})";

    public string SalaireHeureEntete => $"Heure (/{HeuresParJour:0.##})";

    /// <summary>True si l'employé a déjà été payé au moins une fois (suppression contrat désactivée).</summary>
    public bool EmployeDejaPaye => _db.BulletinsPaie.Any(b => b.EmployeId == _employeId);

    public int NbContrats => Contrats.Count;

    /// <summary>Formulaire d'ajout visible uniquement s'il n'y a pas encore de contrat.</summary>
    public bool AfficherFormulaireAjout => Contrats.Count == 0;

    public ObservableCollection<Contrat> Contrats { get; }
    public ObservableCollection<CategorieProfessionnelle> Categories { get; }
    public ObservableCollection<string> TypesContrat { get; }
    public ObservableCollection<string> Devises { get; }

    public Contrat NouveauContrat { get; }

    public Contrat? Selectionne
    {
        get => _selectionne;
        set
        {
            _selectionne = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EmployeDejaPaye));
            (ModifierCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SupprimerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ExporterPdfCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public ICommand AjouterCommand { get; }
    public ICommand ModifierCommand { get; }
    public ICommand SupprimerCommand { get; }
    public ICommand ExporterPdfCommand { get; }

    public Action<string>? OnErreur { get; set; }
    public Action<int>? OnDemandeModification { get; set; }
    public Action<int>? OnDemandeExportPdf { get; set; }
    public Action? OnContratModifie { get; set; }

    public void Charger()
    {
        var employe = _db.Employes.AsNoTracking().FirstOrDefault(e => e.Id == _employeId);
        NomEmploye = employe != null ? $"{employe.Nom} {employe.Prenom}".Trim() : "";
        OnPropertyChanged(nameof(NomEmploye));

        Categories.Clear();
        foreach (var c in _db.CategoriesProfessionnelles.OrderBy(x => x.Libelle))
            Categories.Add(c);

        if (NouveauContrat.CategorieProfessionnelleId <= 0 && Categories.Count > 0)
            NouveauContrat.CategorieProfessionnelleId = Categories[0].Id;

        var entrepriseId = ContexteEntrepriseService.ObtenirEntrepriseIdEmploye(_db, _employeId);
        var politique = new PolitiquePaieService(_db).Charger(entrepriseId);
        JoursReferencePaie = politique.JoursReferencePaie;
        HeuresParJour = politique.HeuresParJour;
        NouveauContrat.JoursReferencePaie = JoursReferencePaie;
        NouveauContrat.HeuresParJour = HeuresParJour;
        OnPropertyChanged(nameof(JoursReferencePaie));
        OnPropertyChanged(nameof(HeuresParJour));
        OnPropertyChanged(nameof(SalaireJourEntete));
        OnPropertyChanged(nameof(SalaireHeureEntete));

        Contrats.Clear();
        foreach (var c in _db.Contrats
            .AsNoTracking()
            .Include(x => x.CategorieProfessionnelle)
            .Where(x => x.EmployeId == _employeId)
            .OrderByDescending(x => x.DateDebut))
        {
            c.JoursReferencePaie = JoursReferencePaie;
            c.HeuresParJour = HeuresParJour;
            Contrats.Add(c);
        }
        var contratIdSelectionne = Selectionne?.Id;
        Selectionne = contratIdSelectionne.HasValue
            ? Contrats.FirstOrDefault(c => c.Id == contratIdSelectionne.Value) ?? Contrats.FirstOrDefault()
            : Contrats.FirstOrDefault();
        OnPropertyChanged(nameof(EmployeDejaPaye));
        OnPropertyChanged(nameof(NbContrats));
        OnPropertyChanged(nameof(AfficherFormulaireAjout));
        (ModifierCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
            UiFeedback.Succes("Contrat créé avec succès.");
        }
        catch (Exception ex) { OnErreur?.Invoke(ex.Message); }
    }

    private void Modifier()
    {
        if (Selectionne is null) return;
        OnDemandeModification?.Invoke(Selectionne.Id);
    }

    private void ExporterPdf()
    {
        if (Selectionne is null) return;
        OnDemandeExportPdf?.Invoke(Selectionne.Id);
    }

    public void NotifierContratModifie()
    {
        Charger();
        OnContratModifie?.Invoke();
    }

    private void Supprimer()
    {
        if (Selectionne is null) return;

        var diagnostic = ContratSuppressionGuard.Analyser(_db, _employeId);
        if (!diagnostic.PeutSupprimer)
        {
            System.Windows.MessageBox.Show(
                diagnostic.Message,
                "Suppression impossible",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var type = Selectionne.TypeContrat;
        var debut = Selectionne.DateDebut.ToString("dd/MM/yyyy");
        var confirm = System.Windows.MessageBox.Show(
            $"Supprimer le contrat {type} (début {debut}) ?\n\nCette action est définitive.",
            "Supprimer un contrat",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (confirm != System.Windows.MessageBoxResult.Yes)
            return;

        if (diagnostic.DemanderConfirmationPrimes)
        {
            var confirmPrimes = System.Windows.MessageBox.Show(
                diagnostic.Message,
                "Primes liées à l'employé",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            if (confirmPrimes != System.Windows.MessageBoxResult.Yes)
                return;
        }

        try
        {
            var entite = _db.Contrats.FirstOrDefault(c => c.Id == Selectionne.Id);
            if (entite != null)
            {
                _db.Contrats.Remove(entite);
                _db.SaveChanges();
                Charger();
                UiFeedback.Succes("Contrat supprimé avec succès.");
            }
        }
        catch (Exception ex) { OnErreur?.Invoke(ex.Message); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
