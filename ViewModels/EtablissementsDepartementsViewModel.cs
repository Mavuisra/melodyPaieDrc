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

/// <summary>
/// CRUD établissements (sites) et départements. Utilise la première entreprise du référentiel.
/// </summary>
public class EtablissementsDepartementsViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private int _entrepriseId;
    private Etablissement? _etablissementSelectionne;
    private Departement? _departementSelectionne;

    public EtablissementsDepartementsViewModel(PaieDbContext db)
    {
        _db = db;
        Etablissements = new ObservableCollection<Etablissement>();
        Departements = new ObservableCollection<Departement>();

        AjouterEtablissementCommand = new RelayCommand(_ => AjouterEtablissement());
        SupprimerEtablissementCommand = new RelayCommand(_ => SupprimerEtablissement(), _ => EtablissementSelectionne != null);
        AjouterDepartementCommand = new RelayCommand(_ => AjouterDepartement(), _ => EtablissementSelectionne != null);
        SupprimerDepartementCommand = new RelayCommand(_ => SupprimerDepartement(), _ => DepartementSelectionne != null);
        EnregistrerCommand = new RelayCommand(_ => Enregistrer());
    }

    public ObservableCollection<Etablissement> Etablissements { get; }
    public ObservableCollection<Departement> Departements { get; }

    public Etablissement? EtablissementSelectionne
    {
        get => _etablissementSelectionne;
        set
        {
            _etablissementSelectionne = value;
            OnPropertyChanged();
            (SupprimerEtablissementCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AjouterDepartementCommand as RelayCommand)?.RaiseCanExecuteChanged();
            ChargerDepartements();
        }
    }

    public Departement? DepartementSelectionne
    {
        get => _departementSelectionne;
        set { _departementSelectionne = value; OnPropertyChanged(); (SupprimerDepartementCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    public ICommand AjouterEtablissementCommand { get; }
    public ICommand SupprimerEtablissementCommand { get; }
    public ICommand AjouterDepartementCommand { get; }
    public ICommand SupprimerDepartementCommand { get; }
    public ICommand EnregistrerCommand { get; }

    public Action? OnFermer { get; set; }
    public Action<string>? OnErreur { get; set; }

    public void Charger()
    {
        _entrepriseId = ContexteEntrepriseService.ObtenirEntrepriseCouranteId(_db);
        if (_entrepriseId <= 0)
        {
            OnErreur?.Invoke("Aucune entreprise active. Terminez la configuration initiale ou sélectionnez une entreprise.");
            return;
        }
        Etablissements.Clear();
        foreach (var e in _db.Etablissements.Include(x => x.Departements).Where(x => x.EntrepriseId == _entrepriseId).OrderBy(x => x.NomSite))
            Etablissements.Add(e);
        EtablissementSelectionne = Etablissements.FirstOrDefault();
    }

    private void ChargerDepartements()
    {
        Departements.Clear();
        if (EtablissementSelectionne == null) return;
        foreach (var d in EtablissementSelectionne.Departements.OrderBy(x => x.NomDepartement))
            Departements.Add(d);
    }

    private void AjouterEtablissement()
    {
        var etab = new Etablissement { EntrepriseId = _entrepriseId, NomSite = "Nouveau site" };
        _db.Etablissements.Add(etab);
        _db.SaveChanges();
        Etablissements.Add(etab);
        EtablissementSelectionne = etab;
    }

    private void SupprimerEtablissement()
    {
        if (EtablissementSelectionne == null) return;
        var etab = EtablissementSelectionne;
        if (etab.Departements?.Any() == true)
        {
            OnErreur?.Invoke("Supprimez d'abord tous les départements de cet établissement.");
            return;
        }
        var entite = _db.Etablissements.Find(etab.Id);
        if (entite != null)
        {
            _db.Etablissements.Remove(entite);
            _db.SaveChanges();
        }
        Etablissements.Remove(etab);
        EtablissementSelectionne = Etablissements.FirstOrDefault();
    }

    private void AjouterDepartement()
    {
        if (EtablissementSelectionne == null) return;
        var dep = new Departement { EtablissementId = EtablissementSelectionne.Id, NomDepartement = "Nouveau département" };
        _db.Departements.Add(dep);
        _db.SaveChanges();
        _db.Entry(dep).Reference(x => x.Etablissement).Load();
        EtablissementSelectionne.Departements?.Add(dep);
        Departements.Add(dep);
    }

    private void SupprimerDepartement()
    {
        if (DepartementSelectionne == null) return;
        var dep = DepartementSelectionne;
        var nbEmployes = _db.Employes.Count(e => e.DepartementId == dep.Id);
        if (nbEmployes > 0)
        {
            OnErreur?.Invoke($"Ce département a {nbEmployes} employé(s). Réaffectez-les avant de le supprimer.");
            return;
        }
        var entite = _db.Departements.Find(dep.Id);
        if (entite != null)
        {
            _db.Departements.Remove(entite);
            _db.SaveChanges();
        }
        EtablissementSelectionne?.Departements?.Remove(dep);
        Departements.Remove(dep);
        DepartementSelectionne = null;
    }

    private void Enregistrer()
    {
        try
        {
            foreach (var e in Etablissements)
            {
                if (string.IsNullOrWhiteSpace(e.NomSite))
                {
                    OnErreur?.Invoke("Tous les établissements doivent avoir un nom.");
                    return;
                }
                var entite = _db.Etablissements.Find(e.Id);
                if (entite != null)
                    entite.NomSite = e.NomSite.Trim();
            }
            foreach (var d in Departements)
            {
                if (string.IsNullOrWhiteSpace(d.NomDepartement))
                {
                    OnErreur?.Invoke("Tous les départements doivent avoir un nom.");
                    return;
                }
                var entite = _db.Departements.Find(d.Id);
                if (entite != null)
                    entite.NomDepartement = d.NomDepartement.Trim();
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
