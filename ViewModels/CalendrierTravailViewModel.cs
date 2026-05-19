using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.ViewModels;

public class CalendrierTravailViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private int _annee;
    private DateTime _nouvelleDate = DateTime.Today;
    private string _nouveauTypeJour = "Ouvre";
    private string? _nouveauLibelle;
    private JourTravailCalendrier? _selectionne;
    private string _typeSemaineSelectionne = "5 jours";

    public CalendrierTravailViewModel(PaieDbContext db)
    {
        _db = db;
        _annee = DateTime.Today.Year;
        TypesJour = new ObservableCollection<string> { "Ouvre", "Ferie", "Repos" };
        TypesSemaine = new ObservableCollection<string> { "5 jours", "6 jours" };
        Jours = new ObservableCollection<JourTravailCalendrier>();
        AnneesDisponibles = new ObservableCollection<int>(Enumerable.Range(_annee - 2, 5));

        AjouterJourCommand = new RelayCommand(_ => AjouterJour(), _ => DroitsUi.PeutModifier);
        SupprimerJourCommand = new RelayCommand(_ => SupprimerJour(), _ => DroitsUi.PeutModifier && Selectionne != null);
        AppliquerTypeSemaineCommand = new RelayCommand(_ => AppliquerTypeSemaine(), _ => DroitsUi.PeutModifier);

        // Charger les jours pour l'année courante
        Charger();
    }

    public ObservableCollection<JourTravailCalendrier> Jours { get; }
    public ObservableCollection<string> TypesJour { get; }
    public ObservableCollection<string> TypesSemaine { get; }
    public ObservableCollection<int> AnneesDisponibles { get; }

    public int Annee
    {
        get => _annee;
        set
        {
            _annee = value;
            OnPropertyChanged();
            if (Jours != null)
                Charger();
        }
    }

    public DateTime NouvelleDate
    {
        get => _nouvelleDate;
        set { _nouvelleDate = value; OnPropertyChanged(); }
    }

    public string NouveauTypeJour
    {
        get => _nouveauTypeJour;
        set { _nouveauTypeJour = value; OnPropertyChanged(); }
    }

    public string? NouveauLibelle
    {
        get => _nouveauLibelle;
        set { _nouveauLibelle = value; OnPropertyChanged(); }
    }

    public JourTravailCalendrier? Selectionne
    {
        get => _selectionne;
        set { _selectionne = value; OnPropertyChanged(); (SupprimerJourCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    public string TypeSemaineSelectionne
    {
        get => _typeSemaineSelectionne;
        set
        {
            if (_typeSemaineSelectionne == value) return;
            _typeSemaineSelectionne = string.IsNullOrWhiteSpace(value) ? "5 jours" : value.Trim();
            OnPropertyChanged();
        }
    }

    public ICommand AjouterJourCommand { get; }
    public ICommand SupprimerJourCommand { get; }
    public ICommand AppliquerTypeSemaineCommand { get; }

    public bool PeutModifier => DroitsUi.PeutModifier;

    public void Charger()
    {
        Jours.Clear();
        foreach (var j in _db.JoursTravailCalendrier
                     .Where(j => j.Annee == Annee)
                     .OrderBy(j => j.DateJour))
        {
            Jours.Add(j);
        }

        TypeSemaineSelectionne = DetecterTypeSemainePourAnnee(Annee);
    }

    private void AjouterJour()
    {
        if (NouvelleDate.Year != Annee)
        {
            NouvelleDate = new DateTime(Annee, NouvelleDate.Month, NouvelleDate.Day);
        }

        // S'il existe déjà un enregistrement pour ce jour, on le remplace
        var existant = _db.JoursTravailCalendrier
            .FirstOrDefault(j => j.Annee == Annee && j.DateJour.Date == NouvelleDate.Date);
        if (existant == null)
        {
            existant = new JourTravailCalendrier
            {
                Annee = Annee,
                DateJour = NouvelleDate.Date
            };
            _db.JoursTravailCalendrier.Add(existant);
        }

        existant.TypeJour = string.IsNullOrWhiteSpace(NouveauTypeJour) ? "Ouvre" : NouveauTypeJour.Trim();
        existant.Libelle = string.IsNullOrWhiteSpace(NouveauLibelle) ? null : NouveauLibelle.Trim();

        _db.SaveChanges();
        Charger();
    }

    private void SupprimerJour()
    {
        if (Selectionne == null) return;
        var entite = _db.JoursTravailCalendrier.Find(Selectionne.Id);
        if (entite != null)
        {
            _db.JoursTravailCalendrier.Remove(entite);
            _db.SaveChanges();
            Charger();
        }
    }

    private void AppliquerTypeSemaine()
    {
        var debut = new DateTime(Annee, 1, 1);
        var fin = new DateTime(Annee, 12, 31);

        var existants = _db.JoursTravailCalendrier
            .Where(j => j.Annee == Annee && j.DateJour >= debut && j.DateJour <= fin)
            .ToDictionary(j => j.DateJour.Date);

        var samediOuvre = TypeSemaineSelectionne == "6 jours";

        for (var d = debut; d <= fin; d = d.AddDays(1))
        {
            if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                continue;

            var typeCible = d.DayOfWeek == DayOfWeek.Sunday
                ? "Repos"
                : (samediOuvre ? "Ouvre" : "Repos");

            if (!existants.TryGetValue(d.Date, out var entite))
            {
                entite = new JourTravailCalendrier
                {
                    Annee = Annee,
                    DateJour = d.Date
                };
                _db.JoursTravailCalendrier.Add(entite);
                existants[d.Date] = entite;
            }

            // On conserve les jours fériés manuellement saisis.
            if (string.Equals(entite.TypeJour, "Ferie", StringComparison.OrdinalIgnoreCase))
                continue;

            entite.TypeJour = typeCible;
            if (typeCible == "Repos" && string.IsNullOrWhiteSpace(entite.Libelle))
                entite.Libelle = null;
        }

        _db.SaveChanges();
        Charger();
    }

    private string DetecterTypeSemainePourAnnee(int annee)
    {
        var samediOuvre = _db.JoursTravailCalendrier
            .Any(j => j.Annee == annee
                      && j.DateJour.DayOfWeek == DayOfWeek.Saturday
                      && j.TypeJour == "Ouvre");

        return samediOuvre ? "6 jours" : "5 jours";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
