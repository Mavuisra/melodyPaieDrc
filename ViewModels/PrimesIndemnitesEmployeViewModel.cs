using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.ViewModels;

public class PrimesIndemnitesEmployeViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private readonly int _employeId;
    private AffectationPrimeIndemnite? _selectionne;

    private PrimeIndemnite? _primeSelectionnee;
    private decimal _montant;

    public PrimesIndemnitesEmployeViewModel(PaieDbContext db, int employeId)
    {
        _db = db;
        _employeId = employeId;
        Affectations = new ObservableCollection<AffectationPrimeIndemnite>();
        PrimesDisponibles = new ObservableCollection<PrimeIndemnite>();

        AjouterCommand = new RelayCommand(_ => Ajouter(), _ => DroitsUi.PeutModifier);
        SupprimerCommand = new RelayCommand(_ => Supprimer(), _ => DroitsUi.PeutModifier && Selectionne != null);
    }

    public bool PeutModifier => DroitsUi.PeutModifier;

    public string NomEmploye { get; set; } = "";

    public ObservableCollection<AffectationPrimeIndemnite> Affectations { get; }
    public ObservableCollection<PrimeIndemnite> PrimesDisponibles { get; }

    public PrimeIndemnite? PrimeSelectionnee
    {
        get => _primeSelectionnee;
        set { _primeSelectionnee = value; OnPropertyChanged(); }
    }

    public decimal Montant
    {
        get => _montant;
        set { _montant = value; OnPropertyChanged(); }
    }

    public AffectationPrimeIndemnite? Selectionne
    {
        get => _selectionne;
        set { _selectionne = value; OnPropertyChanged(); (SupprimerCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    public ICommand AjouterCommand { get; }
    public ICommand SupprimerCommand { get; }

    public Action<string>? OnErreur { get; set; }

    public void Charger()
    {
        var employe = _db.Employes.Find(_employeId);
        NomEmploye = employe != null ? $"{employe.Nom} {employe.Prenom}".Trim() : "";

        PrimesDisponibles.Clear();
        foreach (var p in _db.PrimesIndemnites.OrderBy(x => x.Libelle))
            PrimesDisponibles.Add(p);

        Affectations.Clear();
        foreach (var a in _db.AffectationsPrimesIndemnites
            .Include(a => a.PrimeIndemnite)
            .Where(a => a.EmployeId == _employeId)
            .OrderBy(a => a.PrimeIndemnite!.Libelle))
        {
            Affectations.Add(a);
        }
    }

    private void Ajouter()
    {
        if (PrimeSelectionnee is null)
        {
            OnErreur?.Invoke("Sélectionnez une prime.");
            return;
        }
        if (Montant < 0)
        {
            OnErreur?.Invoke("Le montant doit être positif ou nul.");
            return;
        }
        if (Affectations.Any(a => a.PrimeIndemniteId == PrimeSelectionnee.Id))
        {
            OnErreur?.Invoke("Cette prime est déjà affectée à cet employé.");
            return;
        }

        try
        {
            var a = new AffectationPrimeIndemnite
            {
                EmployeId = _employeId,
                PrimeIndemniteId = PrimeSelectionnee.Id,
                Montant = decimal.Round(Montant, 2)
            };
            _db.AffectationsPrimesIndemnites.Add(a);
            _db.SaveChanges();
            _db.Entry(a).Reference(x => x.PrimeIndemnite).Load();
            Affectations.Add(a);
            Montant = 0;
            OnPropertyChanged(nameof(Montant));
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke(ex.Message);
        }
    }

    private void Supprimer()
    {
        if (Selectionne is null) return;
        try
        {
            var entite = _db.AffectationsPrimesIndemnites.Find(Selectionne.Id);
            if (entite != null)
            {
                _db.AffectationsPrimesIndemnites.Remove(entite);
                _db.SaveChanges();
                Affectations.Remove(Selectionne);
                Selectionne = null;
            }
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke(ex.Message);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
