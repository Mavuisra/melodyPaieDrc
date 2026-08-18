using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.ViewModels;

public class QuinzainesViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private PeriodePaie? _periode;
    private Employe? _employe;
    private QuinzaineOctroi? _selectionne;
    private DateTime _dateOctroi = DateTime.Today;
    private decimal _montant;
    private string _commentaire = "";

    public QuinzainesViewModel(PaieDbContext db, int? periodePaieId = null)
    {
        _db = db;
        Periodes = new ObservableCollection<PeriodePaie>();
        Employes = new ObservableCollection<Employe>();
        Octrois = new ObservableCollection<QuinzaineOctroi>();
        AjouterCommand = new RelayCommand(_ => Ajouter(), _ => DroitsUi.PeutModifier && PeutSaisir);
        EnregistrerCommand = new RelayCommand(_ => Modifier(), _ => DroitsUi.PeutModifier && Selectionne != null && PeutSaisir);
        SupprimerCommand = new RelayCommand(_ => Supprimer(), _ => DroitsUi.PeutModifier && Selectionne != null);
        if (periodePaieId is int id)
            _periode = _db.PeriodesPaie.FirstOrDefault(p => p.Id == id);
    }

    public bool PeutModifier => DroitsUi.PeutModifier;
    public bool PeutSaisir => Periode != null && Employe != null && Montant > 0;

    public ObservableCollection<PeriodePaie> Periodes { get; }
    public ObservableCollection<Employe> Employes { get; }
    public ObservableCollection<QuinzaineOctroi> Octrois { get; }

    public PeriodePaie? Periode
    {
        get => _periode;
        set { _periode = value; OnPropertyChanged(); ChargerOctrois(); (AjouterCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    public Employe? Employe
    {
        get => _employe;
        set { _employe = value; OnPropertyChanged(); (AjouterCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    public QuinzaineOctroi? Selectionne
    {
        get => _selectionne;
        set
        {
            _selectionne = value;
            if (value != null)
            {
                DateOctroi = value.DateOctroi;
                Montant = value.Montant;
                Commentaire = value.Commentaire ?? "";
                Employe = Employes.FirstOrDefault(e => e.Id == value.EmployeId) ?? value.Employe;
            }
            OnPropertyChanged();
            (EnregistrerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SupprimerCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public DateTime DateOctroi { get => _dateOctroi; set { _dateOctroi = value; OnPropertyChanged(); } }
    public decimal Montant
    {
        get => _montant;
        set { _montant = value; OnPropertyChanged(); (AjouterCommand as RelayCommand)?.RaiseCanExecuteChanged(); (EnregistrerCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }
    public string Commentaire { get => _commentaire; set { _commentaire = value ?? ""; OnPropertyChanged(); } }

    public ICommand AjouterCommand { get; }
    public ICommand EnregistrerCommand { get; }
    public ICommand SupprimerCommand { get; }

    public Action<string>? OnErreur { get; set; }
    public Action<string>? OnSucces { get; set; }
    public Func<string, bool>? OnConfirmer { get; set; }
    public Action<IReadOnlyList<QuinzaineOctroi>, PeriodePaie>? OnExporterPdf { get; set; }

    public ICommand ExporterPdfCommand => new RelayCommand(_ =>
    {
        if (Periode == null || Octrois.Count == 0)
        {
            OnErreur?.Invoke("Aucun octroi à exporter pour cette période.");
            return;
        }
        OnExporterPdf?.Invoke(Octrois.ToList(), Periode);
    });

    public void Charger()
    {
        Periodes.Clear();
        foreach (var p in _db.PeriodesPaie.AsNoTracking().OrderByDescending(x => x.Annee).ThenByDescending(x => x.Mois))
            Periodes.Add(p);
        if (Periode != null)
            Periode = Periodes.FirstOrDefault(p => p.Id == Periode.Id) ?? Periodes.FirstOrDefault();
        else
            Periode = Periodes.FirstOrDefault();

        Employes.Clear();
        foreach (var e in _db.Employes.AsNoTracking().Include(x => x.Departement).OrderBy(x => x.Nom).ThenBy(x => x.Prenom))
            Employes.Add(e);

        ChargerOctrois();
    }

    private void ChargerOctrois()
    {
        Octrois.Clear();
        if (Periode == null)
            return;
        foreach (var q in _db.QuinzaineOctrois
                     .Include(x => x.Employe)
                     .Where(x => x.PeriodePaieId == Periode.Id)
                     .OrderByDescending(x => x.DateOctroi)
                     .ToList())
            Octrois.Add(q);
    }

    private bool Valider(out string? erreur)
    {
        erreur = null;
        if (!DroitsUi.PeutModifier)
        {
            erreur = "Vous n’avez pas le droit de modifier les quinzaines.";
            return false;
        }
        if (Periode == null)
        {
            erreur = "Sélectionnez une période de paie.";
            return false;
        }
        if (Employe == null)
        {
            erreur = "Sélectionnez un employé.";
            return false;
        }
        if (Montant <= 0)
        {
            erreur = "Le montant doit être supérieur à 0.";
            return false;
        }
        return true;
    }

    private void Ajouter()
    {
        if (!Valider(out var err))
        {
            OnErreur?.Invoke(err ?? "Formulaire invalide.");
            return;
        }

        try
        {
            var octroi = new QuinzaineOctroi
            {
                EmployeId = Employe!.Id,
                PeriodePaieId = Periode!.Id,
                DateOctroi = DateOctroi.Date,
                Montant = decimal.Round(Montant, 2),
                Commentaire = string.IsNullOrWhiteSpace(Commentaire) ? null : Commentaire.Trim()
            };
            _db.QuinzaineOctrois.Add(octroi);
            _db.SaveChanges();
            QuinzaineOctroiService.SynchroniserAcomptesPeriode(_db, octroi.EmployeId, octroi.PeriodePaieId);
            _db.SaveChanges();
            ChargerOctrois();
            Montant = 0;
            Commentaire = "";
            AppSessionEvents.NotifierDonneesMetierModifiees();
            OnSucces?.Invoke("Quinzaine octroyée.");
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke(ex.Message);
        }
    }

    private void Modifier()
    {
        if (Selectionne == null)
        {
            OnErreur?.Invoke("Sélectionnez un octroi à modifier.");
            return;
        }
        if (!Valider(out var err))
        {
            OnErreur?.Invoke(err ?? "Formulaire invalide.");
            return;
        }

        try
        {
            var entite = _db.QuinzaineOctrois.FirstOrDefault(q => q.Id == Selectionne.Id);
            if (entite == null)
            {
                OnErreur?.Invoke("Octroi introuvable.");
                return;
            }

            var ancienEmploye = entite.EmployeId;
            var anciennePeriode = entite.PeriodePaieId;
            entite.EmployeId = Employe!.Id;
            entite.PeriodePaieId = Periode!.Id;
            entite.DateOctroi = DateOctroi.Date;
            entite.Montant = decimal.Round(Montant, 2);
            entite.Commentaire = string.IsNullOrWhiteSpace(Commentaire) ? null : Commentaire.Trim();
            _db.SaveChanges();
            QuinzaineOctroiService.SynchroniserAcomptesPeriode(_db, entite.EmployeId, entite.PeriodePaieId);
            if (ancienEmploye != entite.EmployeId || anciennePeriode != entite.PeriodePaieId)
                QuinzaineOctroiService.SynchroniserAcomptesPeriode(_db, ancienEmploye, anciennePeriode);
            _db.SaveChanges();
            ChargerOctrois();
            AppSessionEvents.NotifierDonneesMetierModifiees();
            OnSucces?.Invoke("Octroi modifié.");
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke(ex.Message);
        }
    }

    private void Supprimer()
    {
        if (Selectionne == null)
            return;
        if (OnConfirmer?.Invoke($"Supprimer l’octroi de {Selectionne.Montant:N2} du {Selectionne.DateOctroi:dd/MM/yyyy} ?") != true)
            return;

        try
        {
            var entite = _db.QuinzaineOctrois.FirstOrDefault(q => q.Id == Selectionne.Id);
            if (entite == null)
                return;
            var employeId = entite.EmployeId;
            var periodeId = entite.PeriodePaieId;
            _db.QuinzaineOctrois.Remove(entite);
            _db.SaveChanges();
            QuinzaineOctroiService.SynchroniserAcomptesPeriode(_db, employeId, periodeId);
            _db.SaveChanges();
            ChargerOctrois();
            AppSessionEvents.NotifierDonneesMetierModifiees();
            OnSucces?.Invoke("Octroi supprimé.");
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke(ex.Message);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
