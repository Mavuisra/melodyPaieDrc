using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
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
    public const int EtapeIdentiteLegale = 1;
    public const int EtapeIdentiteVisuelle = 2;
    public const int EtapeStructurePaie = 3;
    public const int EtapeAdministrateur = 4;

    private readonly PaieDbContext _db = new();
    private int _entrepriseId;
    private int _etapeCourante = 1;
    private string _messageEtat = "";

    // Étape 1 — identité légale
    private string _raisonSociale = "";
    private string? _nif;
    private string? _nrc;
    private string? _idNat;
    private string? _numCnss;
    private string? _numInpp;
    private string? _numeroAffiliationCnss;
    private string? _adresse;
    private string? _telephone;
    private string? _email;
    private string? _siteWeb;

    // Étape 2 — visuel
    private string? _logo;
    private string? _couleurPrincipale = "#1E3A5F";
    private string? _couleurSecondaire = "#00A6B8";

    // Étape 3 — structure & paie
    private string _nomSite = "Siège";
    private string _nomDepartement = "Direction générale";
    private decimal _tauxCdfParUsd = ParametresApplicationHelper.TauxParDefaut;
    private string _zkTerminalIp = "";
    private string _zkPortText = "4370";

    // Étape 4 — admin
    private string _adminLogin = "";
    private string _adminNomComplet = "Administrateur";
    private string _adminMotDePasse = "";
    private string _adminMotDePasseConfirm = "";

    public AssistantConfigurationViewModel()
    {
        PrecedentCommand = new RelayCommand(_ => EtapePrecedente(), _ => EtapeCourante > 1);
        SuivantCommand = new RelayCommand(_ => EtapeSuivante());
        TerminerCommand = new RelayCommand(_ => Terminer(), _ => EtapeCourante == EtapeAdministrateur);
        ChoisirLogoCommand = new RelayCommand(_ => OnDemandeChoisirLogo?.Invoke());
        SupprimerLogoCommand = new RelayCommand(_ => SupprimerLogo(), _ => !string.IsNullOrEmpty(Logo));
        Charger();
    }

    public string Titre => "Configuration initiale de votre espace";

    public string SousTitreEtape => EtapeCourante switch
    {
        EtapeIdentiteLegale => "Étape 1/4 — Identité légale et coordonnées",
        EtapeIdentiteVisuelle => "Étape 2/4 — Logo et couleurs (bulletins, interface)",
        EtapeStructurePaie => "Étape 3/4 — Structure, paie et pointeuse",
        EtapeAdministrateur => "Étape 4/4 — Compte administrateur",
        _ => ""
    };

    public string IndicateurEtapes =>
        "1 · Légal  →  2 · Visuel  →  3 · Structure & paie  →  4 · Administrateur";

    public int EtapeCourante
    {
        get => _etapeCourante;
        private set
        {
            if (_etapeCourante == value) return;
            _etapeCourante = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SousTitreEtape));
            OnPropertyChanged(nameof(EstEtape1));
            OnPropertyChanged(nameof(EstEtape2));
            OnPropertyChanged(nameof(EstEtape3));
            OnPropertyChanged(nameof(EstEtape4));
            OnPropertyChanged(nameof(AfficherSuivant));
            OnPropertyChanged(nameof(AfficherTerminer));
            (PrecedentCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (TerminerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            if (value == EtapeIdentiteVisuelle)
                EntrepriseBrandingService.AppliquerApercuCouleurs(CouleurPrincipale, CouleurSecondaire);
            if (value == EtapeAdministrateur)
                ActualiserMessageEtat();
        }
    }

    public bool DoitRemplacerMotDePasseParDefaut =>
        AuthService.UnAdministrateurActifUtiliseIdentifiantsParDefaut(_db);

    public bool EstEtape1 => EtapeCourante == EtapeIdentiteLegale;
    public bool EstEtape2 => EtapeCourante == EtapeIdentiteVisuelle;
    public bool EstEtape3 => EtapeCourante == EtapeStructurePaie;
    public bool EstEtape4 => EtapeCourante == EtapeAdministrateur;
    public bool AfficherSuivant => EtapeCourante < EtapeAdministrateur;
    public bool AfficherTerminer => EtapeCourante == EtapeAdministrateur;

    public string MessageEtat
    {
        get => _messageEtat;
        private set { _messageEtat = value; OnPropertyChanged(); }
    }

    public string RaisonSociale { get => _raisonSociale; set { _raisonSociale = value ?? ""; OnPropertyChanged(); } }
    public string? Nif { get => _nif; set { _nif = value; OnPropertyChanged(); } }
    public string? Nrc { get => _nrc; set { _nrc = value; OnPropertyChanged(); } }
    public string? IdNat { get => _idNat; set { _idNat = value; OnPropertyChanged(); } }
    public string? NumCnss { get => _numCnss; set { _numCnss = value; OnPropertyChanged(); } }
    public string? NumInpp { get => _numInpp; set { _numInpp = value; OnPropertyChanged(); } }
    public string? NumeroAffiliationCnss { get => _numeroAffiliationCnss; set { _numeroAffiliationCnss = value; OnPropertyChanged(); } }
    public string? Adresse { get => _adresse; set { _adresse = value; OnPropertyChanged(); } }
    public string? Telephone { get => _telephone; set { _telephone = value; OnPropertyChanged(); } }
    public string? Email { get => _email; set { _email = value; OnPropertyChanged(); } }
    public string? SiteWeb { get => _siteWeb; set { _siteWeb = value; OnPropertyChanged(); } }

    public string? Logo
    {
        get => _logo;
        set { _logo = value; OnPropertyChanged(); OnPropertyChanged(nameof(CheminLogoComplet)); (SupprimerLogoCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    public string? CheminLogoComplet
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Logo)) return null;
            return Path.Combine(PaieDbContext.GetDataDirectory(), Path.GetFileName(Logo));
        }
    }

    public string? CouleurPrincipale
    {
        get => _couleurPrincipale;
        set
        {
            _couleurPrincipale = value;
            OnPropertyChanged();
            if (EtapeCourante == EtapeIdentiteVisuelle)
                EntrepriseBrandingService.AppliquerApercuCouleurs(value, CouleurSecondaire);
        }
    }

    public string? CouleurSecondaire
    {
        get => _couleurSecondaire;
        set
        {
            _couleurSecondaire = value;
            OnPropertyChanged();
            if (EtapeCourante == EtapeIdentiteVisuelle)
                EntrepriseBrandingService.AppliquerApercuCouleurs(CouleurPrincipale, value);
        }
    }

    public string NomSite { get => _nomSite; set { _nomSite = value ?? ""; OnPropertyChanged(); } }
    public string NomDepartement { get => _nomDepartement; set { _nomDepartement = value ?? ""; OnPropertyChanged(); } }
    public decimal TauxCdfParUsd { get => _tauxCdfParUsd; set { _tauxCdfParUsd = value; OnPropertyChanged(); } }
    public string ZkTerminalIp { get => _zkTerminalIp; set { _zkTerminalIp = value ?? ""; OnPropertyChanged(); } }
    public string ZkPortText { get => _zkPortText; set { _zkPortText = value ?? ""; OnPropertyChanged(); } }

    public string AdminLogin { get => _adminLogin; set { _adminLogin = value ?? ""; OnPropertyChanged(); } }
    public string AdminNomComplet { get => _adminNomComplet; set { _adminNomComplet = value ?? ""; OnPropertyChanged(); } }
    public string AdminMotDePasse { get => _adminMotDePasse; set { _adminMotDePasse = value ?? ""; OnPropertyChanged(); } }
    public string AdminMotDePasseConfirm { get => _adminMotDePasseConfirm; set { _adminMotDePasseConfirm = value ?? ""; OnPropertyChanged(); } }

    public ICommand PrecedentCommand { get; }
    public ICommand SuivantCommand { get; }
    public ICommand TerminerCommand { get; }
    public ICommand ChoisirLogoCommand { get; }
    public ICommand SupprimerLogoCommand { get; }

    public Action? OnConfigurationTerminee { get; set; }
    public Action<string>? OnErreur { get; set; }
    public Action? OnDemandeChoisirLogo { get; set; }

    /// <summary>Fourni par la vue (PasswordBox) avant enregistrement de l'étape 4.</summary>
    public Func<(string MotDePasse, string Confirmation)>? ObtenirMotsDePasseAdmin { get; set; }

    public void SynchroniserMotsDePasseDepuisVue()
    {
        if (ObtenirMotsDePasseAdmin == null) return;
        var (pwd, confirm) = ObtenirMotsDePasseAdmin();
        AdminMotDePasse = pwd;
        AdminMotDePasseConfirm = confirm;
    }

    public void Charger()
    {
        ContexteEntrepriseService.InitialiserDepuisBase(_db);
        _entrepriseId = ContexteEntrepriseService.ObtenirEntrepriseCouranteId(_db);

        var ent = _entrepriseId > 0 ? _db.Entreprises.FirstOrDefault(e => e.Id == _entrepriseId) : null;
        if (ent != null)
        {
            RaisonSociale = ent.RaisonSociale;
            Nif = ent.Nif;
            Nrc = ent.Nrc;
            IdNat = ent.IdNat;
            NumCnss = ent.NumCnss;
            NumInpp = ent.NumInpp;
            NumeroAffiliationCnss = ent.NumeroAffiliationCnss;
            Adresse = ent.Adresse;
            Telephone = ent.Telephone;
            Email = ent.Email;
            SiteWeb = ent.SiteWeb;
            Logo = ent.Logo;
            CouleurPrincipale = string.IsNullOrWhiteSpace(ent.CouleurPrincipale) ? "#1E3A5F" : ent.CouleurPrincipale;
            CouleurSecondaire = ent.CouleurSecondaire ?? "#00A6B8";
        }

        TauxCdfParUsd = ParametresApplicationHelper.GetTauxCdfParUsd(_db);
        ParametresApplicationHelper.EnsureRow(_db);
        var p = ParametresApplicationHelper.GetParametresEntrepriseCourante(_db);
        if (p != null)
        {
            ZkTerminalIp = p.ZkTerminalIp ?? "";
            ZkPortText = p.ZkTerminalPort > 0 ? p.ZkTerminalPort.ToString() : "4370";
        }

        var etab = ContexteEntrepriseService.EtablissementsEntrepriseCourante(_db).FirstOrDefault();
        if (etab != null) NomSite = etab.NomSite;
        var dep = ContexteEntrepriseService.DepartementsEntrepriseCourante(_db).FirstOrDefault();
        if (dep != null) NomDepartement = dep.NomDepartement;

        var admin = _db.Utilisateurs.FirstOrDefault(u => u.Role == Utilisateur.RoleAdmin && u.Actif);
        if (admin != null)
        {
            AdminLogin = admin.Login;
            AdminNomComplet = admin.NomComplet ?? "Administrateur";
        }

        MessageEtat = ConfigurationEntrepriseService.Evaluer(_db).Resume;
    }

    public void DefinirLogoDepuisFichier(string cheminSource)
    {
        if (string.IsNullOrWhiteSpace(cheminSource) || !File.Exists(cheminSource)) return;
        var dataDir = PaieDbContext.GetDataDirectory();
        if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
        var ext = Path.GetExtension(cheminSource);
        if (string.IsNullOrEmpty(ext)) ext = ".png";
        var nomFichier = "Logo" + ext;
        var dest = Path.Combine(dataDir, nomFichier);
        try
        {
            File.Copy(cheminSource, dest, true);
            Logo = nomFichier;
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke($"Impossible de copier le logo : {ex.Message}");
        }
    }

    private void SupprimerLogo()
    {
        Logo = null;
        var dataDir = PaieDbContext.GetDataDirectory();
        foreach (var f in new[] { "Logo.png", "Logo.jpg", "Logo.jpeg", "Logo.bmp", "Logo.gif" })
        {
            var path = Path.Combine(dataDir, f);
            if (File.Exists(path)) try { File.Delete(path); } catch { /* ignore */ }
        }
    }

    private void EtapePrecedente() => EtapeCourante = Math.Max(1, EtapeCourante - 1);

    private void EtapeSuivante()
    {
        if (!EnregistrerEtapeCourante())
            return;
        EtapeCourante = Math.Min(EtapeAdministrateur, EtapeCourante + 1);
        ActualiserMessageEtat();
    }

    private void ActualiserMessageEtat()
    {
        if (EtapeCourante == EtapeAdministrateur)
        {
            if (DoitRemplacerMotDePasseParDefaut)
            {
                MessageEtat =
                    "Sécurité : le compte admin/admin par défaut doit être remplacé. " +
                    "Saisissez un identifiant, un mot de passe (8 caractères min., lettre + chiffre) et la même confirmation.";
                return;
            }

            if (AuthService.AdministrateurActifExiste(_db))
            {
                MessageEtat =
                    "Compte administrateur déjà présent. Laissez les mots de passe vides pour conserver l'actuel, " +
                    "ou saisissez un nouveau mot de passe + confirmation pour le modifier.";
                return;
            }

            MessageEtat =
                "Créez le compte de connexion (étape 2/2 après cette fenêtre). Mot de passe : 8 caractères min., lettre + chiffre.";
            return;
        }

        MessageEtat = ConfigurationEntrepriseService.Evaluer(_db).Resume;
    }

    private bool EnregistrerEtapeCourante() => EtapeCourante switch
    {
        EtapeIdentiteLegale => EnregistrerIdentiteLegale(),
        EtapeIdentiteVisuelle => EnregistrerIdentiteVisuelle(),
        EtapeStructurePaie => EnregistrerStructurePaie(),
        EtapeAdministrateur => EnregistrerAdministrateur(),
        _ => false
    };

    /// <summary>Termine la configuration avec les mots de passe lus depuis les PasswordBox (évite perte IME / focus).</summary>
    public void TerminerAvecMotsDePasse(string motDePasse, string confirmation)
    {
        AdminMotDePasse = motDePasse ?? "";
        AdminMotDePasseConfirm = confirmation ?? "";
        Terminer();
    }

    private void Terminer()
    {
        if (!EnregistrerAdministrateur())
            return;

        var etat = ConfigurationEntrepriseService.Evaluer(_db);
        if (!etat.EstComplete)
        {
            OnErreur?.Invoke(etat.Resume);
            return;
        }

        ConfigurationEntrepriseService.MarquerConfigurationTerminee(_db);
        AppSessionEvents.NotifierEntrepriseCouranteChanged();
        OnConfigurationTerminee?.Invoke();
    }

    private bool EnregistrerIdentiteLegale()
    {
        if (string.IsNullOrWhiteSpace(RaisonSociale) ||
            string.Equals(RaisonSociale.Trim(), ConfigurationEntrepriseService.RaisonSocialePlaceholder, StringComparison.OrdinalIgnoreCase))
        {
            OnErreur?.Invoke("La raison sociale est obligatoire (nom réel de l'entreprise).");
            return false;
        }

        Entreprise ent;
        if (_entrepriseId > 0)
            ent = _db.Entreprises.Find(_entrepriseId)!;
        else
        {
            ent = new Entreprise();
            _db.Entreprises.Add(ent);
        }

        ent.RaisonSociale = RaisonSociale.Trim();
        ent.Nif = TrimOrNull(Nif);
        ent.Nrc = TrimOrNull(Nrc);
        ent.IdNat = TrimOrNull(IdNat);
        ent.NumCnss = TrimOrNull(NumCnss);
        ent.NumInpp = TrimOrNull(NumInpp);
        ent.NumeroAffiliationCnss = TrimOrNull(NumeroAffiliationCnss);
        ent.Adresse = TrimOrNull(Adresse);
        ent.Telephone = TrimOrNull(Telephone);
        ent.Email = TrimOrNull(Email);
        ent.SiteWeb = TrimOrNull(SiteWeb);

        _db.SaveChanges();
        _entrepriseId = ent.Id;
        ContexteEntrepriseService.DefinirEntrepriseCourante(_entrepriseId);
        AppSessionEvents.NotifierEntrepriseCouranteChanged();
        return true;
    }

    private bool EnregistrerIdentiteVisuelle()
    {
        if (_entrepriseId <= 0)
        {
            OnErreur?.Invoke("Complétez d'abord l'étape 1 (identité légale).");
            return false;
        }

        var ent = _db.Entreprises.Find(_entrepriseId)!;
        ent.Logo = TrimOrNull(Logo);
        ent.CouleurPrincipale = NormaliserCouleurHex(CouleurPrincipale) ?? "#1E3A5F";
        ent.CouleurSecondaire = NormaliserCouleurHex(CouleurSecondaire);
        _db.SaveChanges();
        AppSessionEvents.NotifierEntrepriseCouranteChanged();
        return true;
    }

    private bool EnregistrerStructurePaie()
    {
        if (_entrepriseId <= 0)
        {
            OnErreur?.Invoke("Complétez d'abord l'étape 1.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(NomSite) || string.IsNullOrWhiteSpace(NomDepartement))
        {
            OnErreur?.Invoke("Le site et le département par défaut sont obligatoires.");
            return false;
        }

        if (TauxCdfParUsd <= 0)
        {
            OnErreur?.Invoke("Le taux CDF/USD doit être supérieur à zéro.");
            return false;
        }

        var etab = ContexteEntrepriseService.EtablissementsEntrepriseCourante(_db).FirstOrDefault();
        if (etab == null)
        {
            etab = new Etablissement { EntrepriseId = _entrepriseId, NomSite = NomSite.Trim() };
            _db.Etablissements.Add(etab);
            _db.SaveChanges();
        }
        else
            etab.NomSite = NomSite.Trim();

        var dep = ContexteEntrepriseService.DepartementsEntrepriseCourante(_db).FirstOrDefault();
        if (dep == null)
        {
            dep = new Departement { EtablissementId = etab.Id, NomDepartement = NomDepartement.Trim() };
            _db.Departements.Add(dep);
        }
        else
            dep.NomDepartement = NomDepartement.Trim();

        ParametresApplicationHelper.SetTauxCdfParUsd(_db, TauxCdfParUsd, mettreAJourPeriodesNonCloturees: true);

        ParametresApplicationHelper.EnsureRow(_db);
        var p = ParametresApplicationHelper.GetParametresEntrepriseCourante(_db);
        p.ZkTerminalIp = string.IsNullOrWhiteSpace(ZkTerminalIp) ? null : ZkTerminalIp.Trim();
        if (int.TryParse(ZkPortText?.Trim(), out var port) && port > 0)
            p.ZkTerminalPort = port;

        if (!_db.PolitiquesPaie.Any(x => x.EntrepriseId == _entrepriseId && x.Actif))
            _db.PolitiquesPaie.Add(DonneesPaieReferenceSeed.CreerPolitiqueParDefaut(_entrepriseId));

        DonneesPaieReferenceSeed.SeedReferentielLegalSiVide(_db, _entrepriseId);
        DonneesPaieReferenceSeed.SeedPrimesCourantesSiVide(_db, _entrepriseId);

        _db.SaveChanges();
        return true;
    }

    private bool EnregistrerAdministrateur()
    {
        if (ObtenirMotsDePasseAdmin != null)
            SynchroniserMotsDePasseDepuisVue();

        if (string.IsNullOrWhiteSpace(AdminLogin))
        {
            OnErreur?.Invoke("L'identifiant administrateur est obligatoire.");
            return false;
        }

        var login = AdminLogin.Trim();
        var motDePasse = AdminMotDePasse ?? "";
        var confirmation = AdminMotDePasseConfirm ?? "";
        var saisieMotDePasse = !string.IsNullOrEmpty(motDePasse) || !string.IsNullOrEmpty(confirmation);
        var adminExiste = AuthService.AdministrateurActifExiste(_db);
        var doitDefinirMotDePasse = !adminExiste || DoitRemplacerMotDePasseParDefaut || saisieMotDePasse;

        if (doitDefinirMotDePasse)
        {
            if (motDePasse != confirmation)
            {
                OnErreur?.Invoke(
                    $"Les mots de passe ne correspondent pas ({motDePasse.Length} caractère(s) saisi(s) / {confirmation.Length} en confirmation). " +
                    "Vérifiez les deux champs « Mot de passe » et « Confirmer ».");
                return false;
            }

            if (!AuthService.ValiderPolitiqueMotDePasse(motDePasse, out var erreurMdp))
            {
                OnErreur?.Invoke(erreurMdp);
                return false;
            }
        }
        else if (!adminExiste)
        {
            OnErreur?.Invoke("Le mot de passe administrateur est obligatoire.");
            return false;
        }

        var existant = _db.Utilisateurs.FirstOrDefault(u => u.Login == login);
        string? hash = null;
        string? salt = null;
        if (doitDefinirMotDePasse)
            (hash, salt) = AuthService.HashMotDePasse(motDePasse);

        if (existant != null)
        {
            if (hash != null && salt != null)
            {
                existant.MotDePasseHash = hash;
                existant.Salt = salt;
            }

            existant.NomComplet = string.IsNullOrWhiteSpace(AdminNomComplet) ? existant.NomComplet : AdminNomComplet.Trim();
            existant.Role = Utilisateur.RoleAdmin;
            existant.Actif = true;
        }
        else
        {
            if (_db.Utilisateurs.Any(u => u.Login == login))
            {
                OnErreur?.Invoke("Cet identifiant est déjà utilisé.");
                return false;
            }

            if (hash == null || salt == null)
            {
                OnErreur?.Invoke("Le mot de passe est obligatoire pour créer ce compte.");
                return false;
            }

            _db.Utilisateurs.Add(new Utilisateur
            {
                Login = login,
                MotDePasseHash = hash,
                Salt = salt,
                NomComplet = string.IsNullOrWhiteSpace(AdminNomComplet) ? "Administrateur" : AdminNomComplet.Trim(),
                Role = Utilisateur.RoleAdmin,
                Actif = true,
                DateCreation = DateTime.UtcNow
            });
        }

        _db.SaveChanges();
        return true;
    }

    private static string? TrimOrNull(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? NormaliserCouleurHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var s = value.Trim();
        if (s.StartsWith('#')) return s.Length >= 4 && s.Length <= 9 ? s : null;
        if (s.Length >= 3 && s.Length <= 8) return "#" + s;
        return null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
