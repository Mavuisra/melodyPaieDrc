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

public class ContratEditViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private readonly int _contratId;

    public ContratEditViewModel(PaieDbContext db, int contratId)
    {
        _db = db;
        _contratId = contratId;
        Contrat = new Contrat();
        Categories = new ObservableCollection<CategorieProfessionnelle>();
        TypesContrat = new ObservableCollection<string> { "CDI", "CDD", "Stage", "Journalier" };
        Devises = new ObservableCollection<string> { "USD", "CDF" };
        EnregistrerCommand = new RelayCommand(_ => Enregistrer(), _ => DroitsUi.PeutModifier);
    }

    public bool PeutModifier => DroitsUi.PeutModifier;

    public string NomEmploye { get; set; } = "";

    public Contrat Contrat { get; }

    public decimal JoursReferencePaie { get; private set; } = SalaireReferenceHelper.JoursDefaut;

    public decimal HeuresParJour { get; private set; } = SalaireReferenceHelper.HeuresDefaut;

    public ObservableCollection<CategorieProfessionnelle> Categories { get; }
    public ObservableCollection<string> TypesContrat { get; }
    public ObservableCollection<string> Devises { get; }

    public ICommand EnregistrerCommand { get; }

    public Action<string>? OnErreur { get; set; }
    public Action? OnEnregistre { get; set; }

    public void Charger()
    {
        var entite = _db.Contrats
            .Include(c => c.CategorieProfessionnelle)
            .FirstOrDefault(c => c.Id == _contratId);
        if (entite == null)
        {
            OnErreur?.Invoke("Contrat introuvable.");
            return;
        }

        var employe = _db.Employes.Find(entite.EmployeId);
        NomEmploye = employe != null ? $"{employe.Nom} {employe.Prenom}".Trim() : "";
        OnPropertyChanged(nameof(NomEmploye));

        Categories.Clear();
        foreach (var c in _db.CategoriesProfessionnelles.OrderBy(x => x.Libelle))
            Categories.Add(c);

        var entrepriseId = ContexteEntrepriseService.ObtenirEntrepriseIdEmploye(_db, entite.EmployeId);
        var politique = new PolitiquePaieService(_db).Charger(entrepriseId);
        JoursReferencePaie = politique.JoursReferencePaie;
        HeuresParJour = politique.HeuresParJour;
        OnPropertyChanged(nameof(JoursReferencePaie));
        OnPropertyChanged(nameof(HeuresParJour));

        CopierVersEdition(entite);
    }

    private void CopierVersEdition(Contrat source)
    {
        Contrat.Id = source.Id;
        Contrat.EmployeId = source.EmployeId;
        Contrat.TypeContrat = source.TypeContrat;
        Contrat.DateDebut = source.DateDebut;
        Contrat.DateFin = source.DateFin;
        Contrat.SalaireBase = source.SalaireBase;
        Contrat.DeviseBase = source.DeviseBase ?? "USD";
        Contrat.CategorieProfessionnelleId = source.CategorieProfessionnelleId;
        Contrat.TauxMajorationHeuresSup = source.TauxMajorationHeuresSup;
        Contrat.TauxMajorationNuit = source.TauxMajorationNuit;
        Contrat.TauxMajorationJourFerie = source.TauxMajorationJourFerie;
        Contrat.PreavisMoisBase = source.PreavisMoisBase;
        Contrat.IndemniteLicenciementMoisBase = source.IndemniteLicenciementMoisBase;
        Contrat.JoursReferencePaie = JoursReferencePaie;
        Contrat.HeuresParJour = HeuresParJour;
        OnPropertyChanged(nameof(Contrat));
    }

    private void Enregistrer()
    {
        if (!Valider(out var message))
        {
            OnErreur?.Invoke(message);
            return;
        }

        try
        {
            var entite = _db.Contrats.Find(_contratId);
            if (entite == null)
            {
                OnErreur?.Invoke("Contrat introuvable.");
                return;
            }

            entite.TypeContrat = Contrat.TypeContrat;
            entite.DateDebut = Contrat.DateDebut;
            entite.DateFin = Contrat.DateFin;
            entite.SalaireBase = Contrat.SalaireBase;
            entite.DeviseBase = Contrat.DeviseBase ?? "USD";
            entite.CategorieProfessionnelleId = Contrat.CategorieProfessionnelleId;
            entite.TauxMajorationHeuresSup = Contrat.TauxMajorationHeuresSup;
            entite.TauxMajorationNuit = Contrat.TauxMajorationNuit;
            entite.TauxMajorationJourFerie = Contrat.TauxMajorationJourFerie;
            entite.PreavisMoisBase = Contrat.PreavisMoisBase;
            entite.IndemniteLicenciementMoisBase = Contrat.IndemniteLicenciementMoisBase;
            _db.SaveChanges();
            UiFeedback.Succes("Contrat modifié avec succès.");
            OnEnregistre?.Invoke();
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke(ex.Message);
        }
    }

    private bool Valider(out string message)
    {
        message = "";
        if (string.IsNullOrWhiteSpace(Contrat.TypeContrat))
        {
            message = "Sélectionnez un type de contrat.";
            return false;
        }
        if (Contrat.SalaireBase <= 0)
        {
            message = "Le salaire de base doit être supérieur à 0.";
            return false;
        }
        if (Contrat.CategorieProfessionnelleId <= 0)
        {
            message = "Sélectionnez une catégorie professionnelle.";
            return false;
        }
        if (string.Equals(Contrat.TypeContrat, "CDI", StringComparison.OrdinalIgnoreCase) && Contrat.DateFin.HasValue)
        {
            message = "Un contrat CDI ne peut pas avoir de date de fin.";
            return false;
        }
        if (!string.Equals(Contrat.TypeContrat, "CDI", StringComparison.OrdinalIgnoreCase) && !Contrat.DateFin.HasValue)
        {
            message = "Une date de fin est obligatoire pour un contrat CDD, Stage ou Journalier.";
            return false;
        }
        if (Contrat.DateFin.HasValue && Contrat.DateFin.Value.Date < Contrat.DateDebut.Date)
        {
            message = "La date de fin doit être postérieure ou égale à la date de début.";
            return false;
        }
        return true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
