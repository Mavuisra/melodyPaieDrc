using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.ViewModels;

public class TauxSociauxViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private TauxSociaux? _selectionne;

    public TauxSociauxViewModel(PaieDbContext db)
    {
        _db = db;
        TauxSociaux = new ObservableCollection<TauxSociaux>();
        AjouterCommand = new RelayCommand(_ => Ajouter());
        SupprimerCommand = new RelayCommand(_ => Supprimer(), _ => Selectionne != null);
        EnregistrerCommand = new RelayCommand(_ => Enregistrer());
    }

    public ObservableCollection<TauxSociaux> TauxSociaux { get; }

    public TauxSociaux? Selectionne
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
        TauxSociaux.Clear();
        foreach (var t in _db.TauxSociaux.OrderBy(x => x.Code))
            TauxSociaux.Add(new TauxSociaux { Id = t.Id, Code = t.Code, Pourcentage = t.Pourcentage });
    }

    private void Ajouter()
    {
        var codes = new[] { "CNSS_Ouvrier", "CNSS_Patronal", "INPP", "ONEM" };
        foreach (var code in codes)
        {
            if (TauxSociaux.Any(t => t.Code == code)) continue;
            TauxSociaux.Add(new TauxSociaux { Code = code, Pourcentage = 0 });
            break;
        }
        if (!codes.Any(c => !TauxSociaux.Any(t => t.Code == c)))
            TauxSociaux.Add(new TauxSociaux { Code = "", Pourcentage = 0 });
    }

    private void Supprimer()
    {
        if (Selectionne is null) return;
        TauxSociaux.Remove(Selectionne);
        Selectionne = null;
    }

    private void Enregistrer()
    {
        try
        {
            var existants = _db.TauxSociaux.ToList();
            foreach (var e in existants)
                _db.TauxSociaux.Remove(e);
            _db.SaveChanges();

            foreach (var t in TauxSociaux.Where(t => !string.IsNullOrWhiteSpace(t.Code)))
            {
                _db.TauxSociaux.Add(new TauxSociaux { Code = t.Code.Trim(), Pourcentage = t.Pourcentage });
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
