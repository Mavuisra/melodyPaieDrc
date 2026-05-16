using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.ViewModels;

public class PeriodesPaieViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private int _mois = DateTime.Today.Month;
    private int _annee = DateTime.Today.Year;
    private decimal _tauxChangeBudget = 3000; // CDF par USD exemple
    private bool _cloturee;

    public PeriodesPaieViewModel(PaieDbContext db)
    {
        _db = db;
        _tauxChangeBudget = ParametresApplicationHelper.GetTauxCdfParUsd(_db);
        Periodes = new ObservableCollection<PeriodePaie>();
        CreerCommand = new RelayCommand(_ => Creer());
        SupprimerCommand = new RelayCommand(_ => Supprimer(), _ => PeriodeSelectionnee != null);
        EnregistrerCommand = new RelayCommand(_ => Enregistrer());
    }

    public ObservableCollection<PeriodePaie> Periodes { get; }

    private PeriodePaie? _periodeSelectionnee;
    public PeriodePaie? PeriodeSelectionnee
    {
        get => _periodeSelectionnee;
        set { _periodeSelectionnee = value; OnPropertyChanged(); (SupprimerCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    public int Mois { get => _mois; set { _mois = value; OnPropertyChanged(); } }
    public int Annee { get => _annee; set { _annee = value; OnPropertyChanged(); } }
    public decimal TauxChangeBudget { get => _tauxChangeBudget; set { _tauxChangeBudget = value; OnPropertyChanged(); } }
    public bool Cloturee { get => _cloturee; set { _cloturee = value; OnPropertyChanged(); } }

    public ICommand CreerCommand { get; }
    public ICommand SupprimerCommand { get; }
    public ICommand EnregistrerCommand { get; }

    public Action? OnFermer { get; set; }
    public Action<string>? OnSucces { get; set; }
    public Action<string>? OnErreur { get; set; }

    public void Charger()
    {
        Periodes.Clear();
        foreach (var p in _db.PeriodesPaie.OrderByDescending(x => x.Annee).ThenByDescending(x => x.Mois))
            Periodes.Add(p);

        TauxChangeBudget = ParametresApplicationHelper.GetTauxCdfParUsd(_db);
    }

    private void Enregistrer()
    {
        try
        {
            _db.SaveChanges();
            OnSucces?.Invoke("Modifications enregistrées.");
        }
        catch (Exception ex) { OnErreur?.Invoke(ex.Message); }
    }

    private void Creer()
    {
        if (Mois < 1 || Mois > 12) { OnErreur?.Invoke("Veuillez saisir un mois valide (1 à 12)."); return; }
        if (Annee < 2000 || Annee > 2100) { OnErreur?.Invoke("Veuillez saisir une année valide (2000 à 2100)."); return; }
        if (TauxChangeBudget <= 0) { OnErreur?.Invoke("Le taux de change (CDF/USD) doit être supérieur à 0."); return; }
        if (_db.PeriodesPaie.Any(p => p.Mois == Mois && p.Annee == Annee))
        {
            OnErreur?.Invoke("Une période pour ce mois/année existe déjà.");
            return;
        }
        try
        {
            _db.PeriodesPaie.Add(new PeriodePaie { Mois = Mois, Annee = Annee, TauxChangeBudget = TauxChangeBudget, Cloturee = Cloturee });
            _db.SaveChanges();
            Charger();
            OnSucces?.Invoke("Période créée avec succès.");
        }
        catch (Exception ex) { OnErreur?.Invoke(ex.Message); }
    }

    private void Supprimer()
    {
        if (PeriodeSelectionnee is null) return;
        var periodeId = PeriodeSelectionnee.Id;
        try
        {
            // Empêche la suppression si des bulletins existent déjà pour cette période.
            // FK BulletinsPaie -> PeriodesPaie est en Restrict, donc SQLite bloquera de toute façon.
            if (_db.BulletinsPaie.Any(b => b.PeriodePaieId == periodeId))
            {
                OnErreur?.Invoke("Impossible de supprimer cette période : des bulletins de paie y sont déjà liés.\n\nSupprimez d'abord les bulletins de cette période (ou créez une nouvelle période) puis réessayez.");
                return;
            }

            var entite = _db.PeriodesPaie.Find(periodeId);
            if (entite != null)
            {
                _db.PeriodesPaie.Remove(entite);
                _db.SaveChanges();
                Charger();
                OnSucces?.Invoke("Période supprimée.");
            }
        }
        catch (DbUpdateException)
        {
            OnErreur?.Invoke("Impossible de supprimer cette période car elle est référencée par d'autres données (ex. bulletins).\n\nVérifiez qu'aucun bulletin n'est lié à cette période, puis réessayez.");
        }
        catch (Exception ex) { OnErreur?.Invoke(ex.Message); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
