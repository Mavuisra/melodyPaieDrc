using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.ViewModels;

public class PretsAvancesViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private readonly int _employeId;
    private PretAvance? _selectionne;
    private int? _pretEnEditionId;

    private decimal _montantTotal;
    private DateTime _dateOctroi = DateTime.Today;
    private DateTime _dateDebutEcheance = DateTime.Today;
    private int _nbEcheances = 1;

    public PretsAvancesViewModel(PaieDbContext db, int employeId)
    {
        _db = db;
        _employeId = employeId;
        PretsAvances = new ObservableCollection<PretAvance>();

        EnregistrerCommand = new RelayCommand(_ => Enregistrer(), _ => DroitsUi.PeutModifier);
        ModifierCommand = new RelayCommand(_ => ChargerPourModification(), _ => DroitsUi.PeutModifier && Selectionne != null);
        AnnulerEditionCommand = new RelayCommand(_ => AnnulerEdition(), _ => EstEnEdition);
        SupprimerCommand = new RelayCommand(_ => Supprimer(), _ => DroitsUi.PeutModifier && Selectionne != null);
    }

    public bool PeutModifier => DroitsUi.PeutModifier;

    public string NomEmploye { get; set; } = "";

    public bool EmployeDejaPaye => _db.BulletinsPaie.Any(b => b.EmployeId == _employeId);

    public bool EstEnEdition => _pretEnEditionId.HasValue;

    public string TitreFormulaire => EstEnEdition
        ? "Modifier le prêt / avance"
        : "Nouveau prêt / avance";

    public string TexteBoutonEnregistrer => EstEnEdition
        ? "Enregistrer les modifications"
        : "Ajouter le prêt / avance";

    public ObservableCollection<PretAvance> PretsAvances { get; }

    public decimal MontantTotal { get => _montantTotal; set { _montantTotal = value; OnPropertyChanged(); } }
    public DateTime DateOctroi
    {
        get => _dateOctroi;
        set
        {
            _dateOctroi = value;
            if (!EstEnEdition && (_dateDebutEcheance == default || (_dateDebutEcheance == DateTime.Today && MontantTotal == 0)))
                DateDebutEcheance = value;
            OnPropertyChanged();
        }
    }
    public DateTime DateDebutEcheance { get => _dateDebutEcheance; set { _dateDebutEcheance = value; OnPropertyChanged(); } }
    public int NbEcheances { get => _nbEcheances; set { _nbEcheances = value < 1 ? 1 : value; OnPropertyChanged(); } }

    public PretAvance? Selectionne
    {
        get => _selectionne;
        set
        {
            _selectionne = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EmployeDejaPaye));
            (ModifierCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SupprimerCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public ICommand EnregistrerCommand { get; }
    public ICommand ModifierCommand { get; }
    public ICommand AnnulerEditionCommand { get; }
    public ICommand SupprimerCommand { get; }

    /// <summary>Remplace la confirmation visuelle (tests). Null = MessageBox.</summary>
    public Func<string, string, bool>? ConfirmerAction { get; set; }

    public Action<string>? OnErreur { get; set; }

    public void Charger()
    {
        var employe = _db.Employes.Find(_employeId);
        NomEmploye = employe != null ? $"{employe.Nom} {employe.Prenom}".Trim() : "";
        OnPropertyChanged(nameof(NomEmploye));

        var idSelectionne = Selectionne?.Id;
        PretsAvances.Clear();
        foreach (var p in _db.PretsAvances
            .Where(p => p.EmployeId == _employeId)
            .OrderByDescending(p => p.DateOctroi))
        {
            PretsAvances.Add(p);
        }

        Selectionne = idSelectionne.HasValue
            ? PretsAvances.FirstOrDefault(p => p.Id == idSelectionne.Value)
            : null;

        OnPropertyChanged(nameof(EmployeDejaPaye));
        (ModifierCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SupprimerCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void ChargerPourModification()
    {
        if (Selectionne is null)
        {
            OnErreur?.Invoke("Sélectionnez un prêt dans le tableau, puis cliquez sur Modifier.");
            return;
        }

        var entite = TrouverPret(Selectionne.Id);
        if (entite is null)
        {
            OnErreur?.Invoke("Ce prêt est introuvable. Actualisez la liste, puis réessayez.");
            return;
        }

        _pretEnEditionId = entite.Id;
        MontantTotal = entite.MontantTotal;
        DateOctroi = entite.DateOctroi;
        DateDebutEcheance = (entite.DateDebutEcheance ?? entite.DateOctroi).Date;
        NbEcheances = entite.NbEcheances < 1 ? 1 : entite.NbEcheances;
        SignalerEditionChangee();
    }

    private void AnnulerEdition()
    {
        ReinitialiserFormulaire();
    }

    private void Enregistrer()
    {
        if (MontantTotal <= 0)
        {
            OnErreur?.Invoke("Le montant total doit être supérieur à 0.");
            return;
        }
        if (NbEcheances < 1)
        {
            OnErreur?.Invoke("Le nombre d'échéances doit être au moins 1.");
            return;
        }
        if (DateDebutEcheance == default)
        {
            OnErreur?.Invoke("La date de début de l’échéance est obligatoire.");
            return;
        }

        try
        {
            if (_pretEnEditionId is int idEdition)
                MettreAJour(idEdition);
            else
                AjouterNouveau();

            Charger();
            ReinitialiserFormulaire();
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke(ex.Message);
        }
    }

    private void AjouterNouveau()
    {
        var montantMensuel = decimal.Round(MontantTotal / NbEcheances, 2);
        _db.PretsAvances.Add(new PretAvance
        {
            EmployeId = _employeId,
            MontantTotal = MontantTotal,
            DateOctroi = DateOctroi,
            DateDebutEcheance = DateDebutEcheance.Date,
            NbEcheances = NbEcheances,
            MontantMensuel = montantMensuel,
            SoldeRestant = MontantTotal,
            Statut = "En cours"
        });
        _db.SaveChanges();
        UiFeedback.Succes("Prêt / avance enregistré(e).");
    }

    private void MettreAJour(int pretId)
    {
        var entite = TrouverPret(pretId);
        if (entite is null)
        {
            OnErreur?.Invoke("Ce prêt est introuvable. Actualisez la liste, puis réessayez.");
            return;
        }

        var dejaPreleve = Math.Max(0m, entite.MontantTotal - entite.SoldeRestant);
        var solde = CalculerSoldeRestant(MontantTotal, dejaPreleve);
        var montantMensuel = decimal.Round(MontantTotal / NbEcheances, 2);

        entite.MontantTotal = MontantTotal;
        entite.DateOctroi = DateOctroi;
        entite.DateDebutEcheance = DateDebutEcheance.Date;
        entite.NbEcheances = NbEcheances;
        entite.MontantMensuel = montantMensuel;
        entite.SoldeRestant = solde;
        entite.Statut = solde <= 0 ? "Terminé" : "En cours";
        _db.SaveChanges();
        UiFeedback.Succes("Prêt / avance modifié(e).");
    }

    private void Supprimer()
    {
        if (Selectionne is null)
        {
            OnErreur?.Invoke("Sélectionnez un prêt dans le tableau, puis cliquez sur Supprimer.");
            return;
        }

        var entite = TrouverPret(Selectionne.Id);
        if (entite is null)
        {
            OnErreur?.Invoke("Ce prêt est introuvable. Actualisez la liste, puis réessayez.");
            return;
        }

        var message = entite.SoldeRestant < entite.MontantTotal
            ? "Des échéances ont déjà été déduites sur des bulletins.\n\n" +
              "Les bulletins déjà générés ne sont pas recalculés. Les prochaines paies n'appliqueront plus cette retenue.\n\n" +
              "Supprimer ce prêt ?"
            : "Supprimer ce prêt / avance ?\n\nLes prochaines paies n'appliqueront plus cette retenue.";

        if (!Confirmer(message, "Supprimer le prêt"))
            return;

        try
        {
            _db.PretsAvances.Remove(entite);
            _db.SaveChanges();
            if (_pretEnEditionId == entite.Id)
                ReinitialiserFormulaire();
            Selectionne = null;
            Charger();
            UiFeedback.Succes("Prêt / avance supprimé(e).");
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke(ex.Message);
        }
    }

    private PretAvance? TrouverPret(int pretId) =>
        _db.PretsAvances
            .IgnoreQueryFilters()
            .FirstOrDefault(p => p.Id == pretId && p.EmployeId == _employeId);

    internal static decimal CalculerSoldeRestant(decimal montantTotal, decimal dejaPreleve) =>
        Math.Max(0m, decimal.Round(montantTotal - dejaPreleve, 2));

    private bool Confirmer(string message, string titre)
    {
        if (ConfirmerAction != null)
            return ConfirmerAction(message, titre);
        return MessageBox.Show(message, titre, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private void ReinitialiserFormulaire()
    {
        _pretEnEditionId = null;
        MontantTotal = 0;
        DateOctroi = DateTime.Today;
        DateDebutEcheance = DateTime.Today;
        NbEcheances = 1;
        SignalerEditionChangee();
    }

    private void SignalerEditionChangee()
    {
        OnPropertyChanged(nameof(EstEnEdition));
        OnPropertyChanged(nameof(TitreFormulaire));
        OnPropertyChanged(nameof(TexteBoutonEnregistrer));
        (AnnulerEditionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (EnregistrerCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
