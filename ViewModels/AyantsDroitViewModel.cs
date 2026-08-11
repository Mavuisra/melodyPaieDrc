using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.ViewModels;

public class AyantsDroitViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private int _employeId;
    private AyantDroit? _selectionne;
    private string _nouveauNom = "";
    private string _nouveauLienParente = "Enfant";
    private DateTime? _nouvelleDateNaissance;
    private string _nomEmploye = "";
    private string _matriculeEmploye = "";

    public AyantsDroitViewModel(PaieDbContext db, int employeId = 0)
    {
        _db = db;
        _employeId = employeId;
        AyantsDroit = new ObservableCollection<AyantDroit>();
        LiensParente = new ObservableCollection<string> { "Enfant", "Conjoint", "Autre" };

        AjouterCommand = new RelayCommand(_ => Ajouter(), _ => PeutModifier && EmployeSelectionne);
        SupprimerCommand = new RelayCommand(_ => Supprimer(), _ => PeutModifier && Selectionne != null);
    }

    public bool PeutModifier => DroitsUi.PeutModifier;

    public bool EmployeSelectionne => _employeId > 0;

    public string NomEmploye
    {
        get => _nomEmploye;
        private set { _nomEmploye = value; OnPropertyChanged(); OnPropertyChanged(nameof(TitreEmploye)); }
    }

    public string MatriculeEmploye
    {
        get => _matriculeEmploye;
        private set { _matriculeEmploye = value; OnPropertyChanged(); OnPropertyChanged(nameof(TitreEmploye)); }
    }

    public string TitreEmploye =>
        EmployeSelectionne
            ? $"{NomEmploye} ({MatriculeEmploye})"
            : "—";

    public string MessageVide =>
        !EmployeSelectionne
            ? "Sélectionnez un employé dans l'onglet Répertoire pour gérer ses ayants droit."
            : "";

    public bool AfficherContenu => EmployeSelectionne;

    /// <summary>True si l'employé a déjà été payé (suppression nécessite confirmation mot de passe admin).</summary>
    public bool EmployeDejaPaye => _employeId > 0 && _db.BulletinsPaie.Any(b => b.EmployeId == _employeId);

    public ObservableCollection<AyantDroit> AyantsDroit { get; }
    public ObservableCollection<string> LiensParente { get; }

    public string NouveauNom { get => _nouveauNom; set { _nouveauNom = value ?? ""; OnPropertyChanged(); } }
    public string NouveauLienParente { get => _nouveauLienParente; set { _nouveauLienParente = value ?? "Enfant"; OnPropertyChanged(); } }
    public DateTime? NouvelleDateNaissance { get => _nouvelleDateNaissance; set { _nouvelleDateNaissance = value; OnPropertyChanged(); } }

    public AyantDroit? Selectionne
    {
        get => _selectionne;
        set { _selectionne = value; OnPropertyChanged(); (SupprimerCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    public ICommand AjouterCommand { get; }
    public ICommand SupprimerCommand { get; }

    public Action<string>? OnErreur { get; set; }

    /// <summary>Demande le mot de passe administrateur (retourne null si annulé).</summary>
    public Func<string?>? OnDemandeMotDePasseAdmin { get; set; }

    public void ChargerPourEmploye(int? employeId)
    {
        _employeId = employeId.GetValueOrDefault();
        Selectionne = null;
        NouveauNom = "";
        NouveauLienParente = "Enfant";
        NouvelleDateNaissance = null;
        Charger();
    }

    public void Charger()
    {
        AyantsDroit.Clear();
        if (_employeId <= 0)
        {
            NomEmploye = "";
            MatriculeEmploye = "";
            NotifierEtat();
            return;
        }

        var employe = _db.Employes.Find(_employeId);
        NomEmploye = employe != null ? $"{employe.Nom} {employe.Postnom} {employe.Prenom}".Trim() : "";
        MatriculeEmploye = employe?.Matricule ?? "";

        foreach (var a in _db.AyantsDroit
            .Where(a => a.EmployeId == _employeId)
            .OrderBy(a => a.LienParente)
            .ThenBy(a => a.Nom))
        {
            AyantsDroit.Add(a);
        }

        NotifierEtat();
    }

    public void NotifierDroitsModification()
    {
        OnPropertyChanged(nameof(PeutModifier));
        NotifierEtat();
    }

    private void NotifierEtat()
    {
        OnPropertyChanged(nameof(EmployeSelectionne));
        OnPropertyChanged(nameof(AfficherContenu));
        OnPropertyChanged(nameof(MessageVide));
        OnPropertyChanged(nameof(EmployeDejaPaye));
        (AjouterCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SupprimerCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void Ajouter()
    {
        if (_employeId <= 0) return;
        if (string.IsNullOrWhiteSpace(NouveauNom))
        {
            OnErreur?.Invoke("Le nom de l'ayant droit est obligatoire.");
            return;
        }
        if (string.IsNullOrWhiteSpace(NouveauLienParente))
        {
            OnErreur?.Invoke("Sélectionnez un lien de parenté (ex. Enfant pour la réduction IPR).");
            return;
        }

        try
        {
            _db.AyantsDroit.Add(new AyantDroit
            {
                EmployeId = _employeId,
                Nom = NouveauNom.Trim(),
                LienParente = NouveauLienParente.Trim(),
                DateNaissance = NouvelleDateNaissance
            });
            _db.SaveChanges();
            AppSessionEvents.NotifierDonneesMetierModifiees();
            Charger();
            NouveauNom = "";
            NouveauLienParente = "Enfant";
            NouvelleDateNaissance = null;
            UiFeedback.Succes("Ayant droit ajouté.");
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke(ex.Message);
        }
    }

    private void Supprimer()
    {
        if (Selectionne is null) return;
        if (EmployeDejaPaye)
        {
            var motDePasse = OnDemandeMotDePasseAdmin?.Invoke();
            if (motDePasse == null) return;
            var user = AuthService.UtilisateurCourant;
            if (user == null || !AuthService.VerifierMotDePasse(motDePasse, user.MotDePasseHash, user.Salt))
            {
                OnErreur?.Invoke("Mot de passe incorrect.");
                return;
            }
        }
        try
        {
            var entite = _db.AyantsDroit.Find(Selectionne.Id);
            if (entite != null)
            {
                _db.AyantsDroit.Remove(entite);
                _db.SaveChanges();
                AppSessionEvents.NotifierDonneesMetierModifiees();
                Charger();
                UiFeedback.Succes("Ayant droit supprimé.");
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
