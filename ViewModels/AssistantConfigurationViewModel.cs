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

public class AssistantConfigurationViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db = new();
    private int _entrepriseId;
    private string _raisonSociale = "";
    private string? _nif;
    private string? _adresse;
    private string _nomSite = "Siège";
    private string _nomDepartement = "Direction générale";
    private decimal _tauxCdfParUsd = ParametresApplicationHelper.TauxParDefaut;
    private string _messageEtat = "";
    private bool _identiteOk;
    private bool _structureOk;
    private bool _politiqueOk;

    public AssistantConfigurationViewModel()
    {
        EnregistrerEtape1Command = new RelayCommand(_ => EnregistrerEtape1());
        EnregistrerEtape2Command = new RelayCommand(_ => EnregistrerEtape2());
        EnregistrerEtape3Command = new RelayCommand(_ => EnregistrerEtape3());
        TerminerCommand = new RelayCommand(_ => Terminer(), _ => PeutTerminer);
        Charger();
    }

    public string Titre => "Configuration initiale — Melody Paie RDC";

    public string MessageEtat
    {
        get => _messageEtat;
        private set { _messageEtat = value; OnPropertyChanged(); }
    }

    public bool IdentiteOk
    {
        get => _identiteOk;
        private set { _identiteOk = value; OnPropertyChanged(); OnPropertyChanged(nameof(PeutTerminer)); }
    }

    public bool StructureOk
    {
        get => _structureOk;
        private set { _structureOk = value; OnPropertyChanged(); OnPropertyChanged(nameof(PeutTerminer)); }
    }

    public bool PolitiqueOk
    {
        get => _politiqueOk;
        private set { _politiqueOk = value; OnPropertyChanged(); OnPropertyChanged(nameof(PeutTerminer)); }
    }

    public bool PeutTerminer => IdentiteOk && StructureOk && PolitiqueOk;

    public string RaisonSociale
    {
        get => _raisonSociale;
        set { _raisonSociale = value ?? ""; OnPropertyChanged(); }
    }

    public string? Nif
    {
        get => _nif;
        set { _nif = value; OnPropertyChanged(); }
    }

    public string? Adresse
    {
        get => _adresse;
        set { _adresse = value; OnPropertyChanged(); }
    }

    public string NomSite
    {
        get => _nomSite;
        set { _nomSite = value ?? ""; OnPropertyChanged(); }
    }

    public string NomDepartement
    {
        get => _nomDepartement;
        set { _nomDepartement = value ?? ""; OnPropertyChanged(); }
    }

    public decimal TauxCdfParUsd
    {
        get => _tauxCdfParUsd;
        set { _tauxCdfParUsd = value; OnPropertyChanged(); }
    }

    public ObservableCollection<Entreprise> EntreprisesDisponibles { get; } = new();

    public Entreprise? EntrepriseSelectionnee { get; set; }

    public ICommand EnregistrerEtape1Command { get; }
    public ICommand EnregistrerEtape2Command { get; }
    public ICommand EnregistrerEtape3Command { get; }
    public ICommand TerminerCommand { get; }

    public Action? OnConfigurationTerminee { get; set; }
    public Action<string>? OnErreur { get; set; }

    public void Charger()
    {
        ContexteEntrepriseService.InitialiserDepuisBase(_db);
        EntreprisesDisponibles.Clear();
        foreach (var e in _db.Entreprises.AsNoTracking().OrderBy(x => x.RaisonSociale))
            EntreprisesDisponibles.Add(e);

        _entrepriseId = ContexteEntrepriseService.ObtenirEntrepriseCouranteId(_db);
        var ent = _db.Entreprises.FirstOrDefault(e => e.Id == _entrepriseId);
        if (ent != null)
        {
            RaisonSociale = ent.RaisonSociale;
            Nif = ent.Nif;
            Adresse = ent.Adresse;
        }

        TauxCdfParUsd = ParametresApplicationHelper.GetTauxCdfParUsd(_db);
        var etab = ContexteEntrepriseService.EtablissementsEntrepriseCourante(_db).FirstOrDefault();
        if (etab != null)
            NomSite = etab.NomSite;
        var dep = ContexteEntrepriseService.DepartementsEntrepriseCourante(_db).FirstOrDefault();
        if (dep != null)
            NomDepartement = dep.NomDepartement;

        RafraichirEtat();
    }

    private void RafraichirEtat()
    {
        var etat = ConfigurationEntrepriseService.Evaluer(_db);
        IdentiteOk = etat.IdentiteRenseignee;
        StructureOk = etat.StructureOrganisationnelle;
        PolitiqueOk = etat.PolitiquePaieActive;
        MessageEtat = etat.Resume;
        (TerminerCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void EnregistrerEtape1()
    {
        if (string.IsNullOrWhiteSpace(RaisonSociale) ||
            string.Equals(RaisonSociale.Trim(), ConfigurationEntrepriseService.RaisonSocialePlaceholder, StringComparison.OrdinalIgnoreCase))
        {
            OnErreur?.Invoke("Indiquez la raison sociale réelle de votre entreprise (pas le libellé par défaut).");
            return;
        }

        Entreprise ent;
        if (_entrepriseId > 0)
        {
            ent = _db.Entreprises.Find(_entrepriseId)!;
            ent.RaisonSociale = RaisonSociale.Trim();
            ent.Nif = string.IsNullOrWhiteSpace(Nif) ? null : Nif.Trim();
            ent.Adresse = string.IsNullOrWhiteSpace(Adresse) ? null : Adresse.Trim();
        }
        else
        {
            ent = new Entreprise
            {
                RaisonSociale = RaisonSociale.Trim(),
                Nif = string.IsNullOrWhiteSpace(Nif) ? null : Nif.Trim(),
                Adresse = string.IsNullOrWhiteSpace(Adresse) ? null : Adresse.Trim()
            };
            _db.Entreprises.Add(ent);
            _db.SaveChanges();
            _entrepriseId = ent.Id;
            ContexteEntrepriseService.DefinirEntrepriseCourante(_entrepriseId);
        }

        _db.SaveChanges();
        RafraichirEtat();
        MessageEtat = "Étape 1 enregistrée. Passez à la structure organisationnelle.";
    }

    private void EnregistrerEtape2()
    {
        if (_entrepriseId <= 0)
        {
            OnErreur?.Invoke("Complétez d'abord l'identité de l'entreprise.");
            return;
        }

        if (string.IsNullOrWhiteSpace(NomSite) || string.IsNullOrWhiteSpace(NomDepartement))
        {
            OnErreur?.Invoke("Le nom du site et du département sont obligatoires.");
            return;
        }

        var etab = ContexteEntrepriseService.EtablissementsEntrepriseCourante(_db).FirstOrDefault();
        if (etab == null)
        {
            etab = new Etablissement { EntrepriseId = _entrepriseId, NomSite = NomSite.Trim() };
            _db.Etablissements.Add(etab);
            _db.SaveChanges();
        }
        else
        {
            etab.NomSite = NomSite.Trim();
        }

        var dep = ContexteEntrepriseService.DepartementsEntrepriseCourante(_db).FirstOrDefault();
        if (dep == null)
        {
            dep = new Departement { EtablissementId = etab.Id, NomDepartement = NomDepartement.Trim() };
            _db.Departements.Add(dep);
        }
        else
        {
            dep.NomDepartement = NomDepartement.Trim();
        }

        _db.SaveChanges();
        RafraichirEtat();
        MessageEtat = "Étape 2 enregistrée. Définissez le taux de change et la politique de paie.";
    }

    private void EnregistrerEtape3()
    {
        if (_entrepriseId <= 0)
        {
            OnErreur?.Invoke("Complétez les étapes précédentes.");
            return;
        }

        if (TauxCdfParUsd <= 0)
        {
            OnErreur?.Invoke("Le taux CDF/USD doit être supérieur à zéro.");
            return;
        }

        ParametresApplicationHelper.SetTauxCdfParUsd(_db, TauxCdfParUsd, mettreAJourPeriodesNonCloturees: true);

        if (!_db.PolitiquesPaie.Any(p => p.EntrepriseId == _entrepriseId && p.Actif))
        {
            _db.PolitiquesPaie.Add(DonneesPaieReferenceSeed.CreerPolitiqueParDefaut(_entrepriseId));
            _db.SaveChanges();
        }

        DonneesPaieReferenceSeed.SeedReferentielLegalSiVide(_db, _entrepriseId);
        DonneesPaieReferenceSeed.SeedPrimesCourantesSiVide(_db, _entrepriseId);

        RafraichirEtat();
        MessageEtat = PolitiqueOk
            ? "Étape 3 enregistrée. Vous pouvez terminer la configuration."
            : "Étape 3 partiellement enregistrée.";
    }

    private void Terminer()
    {
        if (!PeutTerminer)
        {
            OnErreur?.Invoke(ConfigurationEntrepriseService.Evaluer(_db).Resume);
            return;
        }

        OnConfigurationTerminee?.Invoke();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
