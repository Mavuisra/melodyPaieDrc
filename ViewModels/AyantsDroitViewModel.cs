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
    private readonly int _employeId;
    private AyantDroit? _selectionne;
    private string _nouveauNom = "";
    private string _nouveauLienParente = "Enfant";
    private DateTime? _nouvelleDateNaissance;

    public AyantsDroitViewModel(PaieDbContext db, int employeId)
    {
        _db = db;
        _employeId = employeId;
        AyantsDroit = new ObservableCollection<AyantDroit>();
        LiensParente = new ObservableCollection<string> { "Enfant", "Conjoint", "Autre" };

        AjouterCommand = new RelayCommand(_ => Ajouter(), _ => DroitsUi.PeutModifier);
        SupprimerCommand = new RelayCommand(_ => Supprimer(), _ => DroitsUi.PeutModifier && Selectionne != null);
    }

    public bool PeutModifier => DroitsUi.PeutModifier;

    public string NomEmploye { get; set; } = "";

    /// <summary>True si l'employé a déjà été payé (suppression nécessite confirmation mot de passe admin).</summary>
    public bool EmployeDejaPaye => _db.BulletinsPaie.Any(b => b.EmployeId == _employeId);

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

    public void Charger()
    {
        var employe = _db.Employes.Find(_employeId);
        NomEmploye = employe != null ? $"{employe.Nom} {employe.Prenom}".Trim() : "";

        AyantsDroit.Clear();
        foreach (var a in _db.AyantsDroit
            .Where(a => a.EmployeId == _employeId)
            .OrderBy(a => a.LienParente)
            .ThenBy(a => a.Nom))
        {
            AyantsDroit.Add(a);
        }
        OnPropertyChanged(nameof(EmployeDejaPaye));
    }

    private void Ajouter()
    {
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
            Charger();
            NouveauNom = "";
            NouveauLienParente = "Enfant";
            NouvelleDateNaissance = null;
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
            if (motDePasse == null) return; // Annulé
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
