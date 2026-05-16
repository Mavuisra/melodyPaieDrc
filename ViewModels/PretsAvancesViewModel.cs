using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.ViewModels;

public class PretsAvancesViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private readonly int _employeId;
    private PretAvance? _selectionne;

    private decimal _montantTotal;
    private DateTime _dateOctroi = DateTime.Today;
    private int _nbEcheances = 1;

    public PretsAvancesViewModel(PaieDbContext db, int employeId)
    {
        _db = db;
        _employeId = employeId;
        PretsAvances = new ObservableCollection<PretAvance>();

        AjouterCommand = new RelayCommand(_ => Ajouter());
        SupprimerCommand = new RelayCommand(_ => Supprimer(), _ => Selectionne != null && !EmployeDejaPaye);
    }

    public string NomEmploye { get; set; } = "";

    /// <summary>True si l'employé a déjà été payé au moins une fois (suppression définitivement désactivée).</summary>
    public bool EmployeDejaPaye => _db.BulletinsPaie.Any(b => b.EmployeId == _employeId);

    public ObservableCollection<PretAvance> PretsAvances { get; }

    public decimal MontantTotal { get => _montantTotal; set { _montantTotal = value; OnPropertyChanged(); } }
    public DateTime DateOctroi { get => _dateOctroi; set { _dateOctroi = value; OnPropertyChanged(); } }
    public int NbEcheances { get => _nbEcheances; set { _nbEcheances = value < 1 ? 1 : value; OnPropertyChanged(); } }

    public PretAvance? Selectionne
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

        PretsAvances.Clear();
        foreach (var p in _db.PretsAvances
            .Where(p => p.EmployeId == _employeId)
            .OrderByDescending(p => p.DateOctroi))
        {
            PretsAvances.Add(p);
        }
        OnPropertyChanged(nameof(EmployeDejaPaye));
        (SupprimerCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void Ajouter()
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

        try
        {
            var montantMensuel = decimal.Round(MontantTotal / NbEcheances, 2);
            _db.PretsAvances.Add(new PretAvance
            {
                EmployeId = _employeId,
                MontantTotal = MontantTotal,
                DateOctroi = DateOctroi,
                NbEcheances = NbEcheances,
                MontantMensuel = montantMensuel,
                SoldeRestant = MontantTotal,
                Statut = "En cours"
            });
            _db.SaveChanges();
            Charger();
            MontantTotal = 0;
            DateOctroi = DateTime.Today;
            NbEcheances = 1;
            OnPropertyChanged(nameof(MontantTotal));
            OnPropertyChanged(nameof(DateOctroi));
            OnPropertyChanged(nameof(NbEcheances));
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
            var entite = _db.PretsAvances.Find(Selectionne.Id);
            if (entite != null)
            {
                _db.PretsAvances.Remove(entite);
                _db.SaveChanges();
                Charger();
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
