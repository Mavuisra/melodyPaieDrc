using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.ViewModels;

public class AbsencesCongesViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private readonly int _employeId;
    private AbsenceConge? _selectionne;

    private string _type = "Annuel";
    private DateTime _dateDebut = DateTime.Today;
    private DateTime _dateFin = DateTime.Today;
    private bool _estPaye = true;

    public AbsencesCongesViewModel(PaieDbContext db, int employeId)
    {
        _db = db;
        _employeId = employeId;
        AbsencesConges = new ObservableCollection<AbsenceConge>();
        // Typologie plus proche de la pratique en RDC
        TypesAbsence = new ObservableCollection<string>
        {
            "Congé annuel",
            "Congé circonstanciel",
            "Maladie",
            "Maternité",
            "Mission",
            "Suspension du contrat",
            "Sans solde",
            "Autre"
        };

        AjouterCommand = new RelayCommand(_ => Ajouter(), _ => DroitsUi.PeutModifier);
        SupprimerCommand = new RelayCommand(_ => Supprimer(), _ => DroitsUi.PeutModifier && Selectionne != null);
    }

    public bool PeutModifier => DroitsUi.PeutModifier;

    public string NomEmploye { get; set; } = "";

    public ObservableCollection<AbsenceConge> AbsencesConges { get; }
    public ObservableCollection<string> TypesAbsence { get; }

    public string Type
    {
        get => _type;
        set
        {
            _type = value ?? "Congé annuel";
            // Règles par défaut de maintien de salaire (modifiable par l'utilisateur via la case à cocher)
            switch (_type)
            {
                case "Congé annuel":
                case "Congé circonstanciel":
                case "Maladie":
                case "Maternité":
                case "Mission":
                    EstPaye = true;
                    break;
                case "Sans solde":
                case "Suspension du contrat":
                    EstPaye = false;
                    break;
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(EstPaye));
        }
    }
    public DateTime DateDebut { get => _dateDebut; set { _dateDebut = value; OnPropertyChanged(); } }
    public DateTime DateFin { get => _dateFin; set { _dateFin = value; OnPropertyChanged(); } }
    public bool EstPaye { get => _estPaye; set { _estPaye = value; OnPropertyChanged(); } }

    public AbsenceConge? Selectionne
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

        AbsencesConges.Clear();
        foreach (var a in _db.AbsencesConges
            .Where(a => a.EmployeId == _employeId)
            .OrderByDescending(a => a.DateDebut))
        {
            AbsencesConges.Add(a);
        }
    }

    private void Ajouter()
    {
        if (string.IsNullOrWhiteSpace(Type))
        {
            OnErreur?.Invoke("Sélectionnez un type d'absence.");
            return;
        }
        if (DateFin < DateDebut)
        {
            OnErreur?.Invoke("La date de fin doit être >= date de début.");
            return;
        }

        try
        {
            _db.AbsencesConges.Add(new AbsenceConge
            {
                EmployeId = _employeId,
                Type = Type.Trim(),
                DateDebut = DateDebut,
                DateFin = DateFin,
                EstPaye = EstPaye
            });
            _db.SaveChanges();
            Charger();
            Type = "Annuel";
            DateDebut = DateTime.Today;
            DateFin = DateTime.Today;
            EstPaye = true;
            OnPropertyChanged(nameof(Type));
            OnPropertyChanged(nameof(DateDebut));
            OnPropertyChanged(nameof(DateFin));
            OnPropertyChanged(nameof(EstPaye));
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
            var entite = _db.AbsencesConges.Find(Selectionne.Id);
            if (entite != null)
            {
                _db.AbsencesConges.Remove(entite);
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
