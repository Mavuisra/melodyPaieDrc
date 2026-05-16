using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.ViewModels;

public class ConfigPrimesIndemnitesViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private PrimeIndemnite? _selectionne;

    public ConfigPrimesIndemnitesViewModel(PaieDbContext db)
    {
        _db = db;
        PrimesIndemnites = new ObservableCollection<PrimeIndemnite>();
        AjouterCommand = new RelayCommand(_ => Ajouter());
        SupprimerCommand = new RelayCommand(_ => Supprimer(), _ => Selectionne != null);
        EnregistrerCommand = new RelayCommand(_ => Enregistrer());
    }

    public ObservableCollection<PrimeIndemnite> PrimesIndemnites { get; }

    public PrimeIndemnite? Selectionne
    {
        get => _selectionne;
        set { _selectionne = value; OnPropertyChanged(); (SupprimerCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    public ICommand AjouterCommand { get; }
    public ICommand SupprimerCommand { get; }
    public ICommand EnregistrerCommand { get; }

    public Action? OnFermer { get; set; }
    public Action<string>? OnErreur { get; set; }

    public void Charger()
    {
        PrimesIndemnites.Clear();
        foreach (var p in _db.PrimesIndemnites.OrderBy(x => x.Libelle))
            PrimesIndemnites.Add(new PrimeIndemnite
            {
                Id = p.Id,
                Libelle = p.Libelle,
                EstImposable = p.EstImposable,
                EstCotisable = p.EstCotisable,
                ModeCalcul = string.IsNullOrWhiteSpace(p.ModeCalcul) ? PrimeIndemnite.ModeFixe : p.ModeCalcul,
                TypeLigne = string.IsNullOrWhiteSpace(p.TypeLigne) ? PrimeIndemnite.TypeAvantage : p.TypeLigne,
                NumeroCompte = p.NumeroCompte
            });
    }

    private void Ajouter()
    {
        PrimesIndemnites.Add(new PrimeIndemnite
        {
            Libelle = "Nouvelle prime",
            EstImposable = true,
            EstCotisable = true,
            ModeCalcul = PrimeIndemnite.ModeFixe,
            TypeLigne = PrimeIndemnite.TypeAvantage
        });
    }

    private void Supprimer()
    {
        if (Selectionne is null) return;
        if (Selectionne.Id != 0 && _db.AffectationsPrimesIndemnites.Any(a => a.PrimeIndemniteId == Selectionne.Id))
        {
            OnErreur?.Invoke("Cette prime est utilisée par des affectations. Supprimez d'abord les affectations.");
            return;
        }
        if (Selectionne.Id != 0)
        {
            var entite = _db.PrimesIndemnites.Find(Selectionne.Id);
            if (entite != null) { _db.PrimesIndemnites.Remove(entite); _db.SaveChanges(); }
        }
        PrimesIndemnites.Remove(Selectionne);
        Selectionne = null;
    }

    private void Enregistrer()
    {
        try
        {
            foreach (var p in PrimesIndemnites.Where(p => string.IsNullOrWhiteSpace(p.Libelle)))
            {
                OnErreur?.Invoke("Toutes les primes doivent avoir un libellé.");
                return;
            }
            var existants = _db.PrimesIndemnites.ToList();
            foreach (var p in PrimesIndemnites)
            {
                if (p.Id == 0)
                    _db.PrimesIndemnites.Add(new PrimeIndemnite
                    {
                        Libelle = p.Libelle.Trim(),
                        EstImposable = p.EstImposable,
                        EstCotisable = p.EstCotisable,
                        ModeCalcul = string.IsNullOrWhiteSpace(p.ModeCalcul) ? PrimeIndemnite.ModeFixe : p.ModeCalcul,
                        TypeLigne = string.IsNullOrWhiteSpace(p.TypeLigne) ? PrimeIndemnite.TypeAvantage : p.TypeLigne,
                        NumeroCompte = p.NumeroCompte?.Trim()
                    });
                else
                {
                    var e = existants.FirstOrDefault(x => x.Id == p.Id);
                    if (e != null)
                    {
                        e.Libelle = p.Libelle.Trim();
                        e.EstImposable = p.EstImposable;
                        e.EstCotisable = p.EstCotisable;
                        e.ModeCalcul = string.IsNullOrWhiteSpace(p.ModeCalcul) ? PrimeIndemnite.ModeFixe : p.ModeCalcul;
                        e.TypeLigne = string.IsNullOrWhiteSpace(p.TypeLigne) ? PrimeIndemnite.TypeAvantage : p.TypeLigne;
                        e.NumeroCompte = p.NumeroCompte?.Trim();
                    }
                }
            }
            _db.SaveChanges();
            OnFermer?.Invoke();
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke(ex.Message);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
