using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.ViewModels;

public class AbsencesCongesViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private readonly int _employeId;
    private AbsenceConge? _selectionne;

    private string _type = "Congé annuel";
    private DateTime _dateDebut = DateTime.Today;
    private DateTime _dateFin = DateTime.Today;
    private bool _estPaye = true;
    private string _dateEmbaucheLibelle = "Non renseignée";
    private string _ancienneteLibelle = "—";
    private string _droitsCongeLibelle = "—";

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

    public string DateEmbaucheLibelle { get => _dateEmbaucheLibelle; private set { _dateEmbaucheLibelle = value; OnPropertyChanged(); } }
    public string AncienneteLibelle { get => _ancienneteLibelle; private set { _ancienneteLibelle = value; OnPropertyChanged(); } }
    public string DroitsCongeLibelle { get => _droitsCongeLibelle; private set { _droitsCongeLibelle = value; OnPropertyChanged(); } }

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
        OnPropertyChanged(nameof(NomEmploye));

        var contrats = _db.Contrats.Where(c => c.EmployeId == _employeId).ToList();
        var embauche = AncienneteCongeHelper.ResoudreDateEmbauche(contrats);
        if (embauche == null)
        {
            DateEmbaucheLibelle = "Non renseignée (aucun contrat)";
            AncienneteLibelle = "—";
            DroitsCongeLibelle = "0 jour (date d’embauche manquante)";
        }
        else
        {
            DateEmbaucheLibelle = embauche.Value.ToString("dd/MM/yyyy");
            AncienneteLibelle = AncienneteCongeHelper.FormaterAnciennete(embauche.Value);
            var jours = AncienneteCongeHelper.CalculerJoursCongesAnnuels(embauche.Value);
            DroitsCongeLibelle = $"{jours:N1} jour(s) de congé annuel (1,5 j / mois d’ancienneté)";
        }

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
            var absence = new AbsenceConge
            {
                EmployeId = _employeId,
                Type = Type.Trim(),
                DateDebut = DateDebut,
                DateFin = DateFin,
                EstPaye = EstPaye
            };
            _db.AbsencesConges.Add(absence);
            _db.SaveChanges();
            AbsenceCongeSuiviSyncService.SynchroniserAbsence(_db, absence);
            AppSessionEvents.NotifierDonneesMetierModifiees();
            Charger();
            Type = "Congé annuel";
            DateDebut = DateTime.Today;
            DateFin = DateTime.Today;
            EstPaye = true;
            OnPropertyChanged(nameof(Type));
            OnPropertyChanged(nameof(DateDebut));
            OnPropertyChanged(nameof(DateFin));
            OnPropertyChanged(nameof(EstPaye));
            UiFeedback.Succes("Absence / congé enregistré(e).");
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
                AbsenceCongeSuiviSyncService.RetirerAbsence(_db, entite);
                _db.AbsencesConges.Remove(entite);
                _db.SaveChanges();
                AppSessionEvents.NotifierDonneesMetierModifiees();
                Charger();
                UiFeedback.Succes("Absence / congé supprimé(e).");
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
