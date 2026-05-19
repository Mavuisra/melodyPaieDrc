using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.ViewModels;

/// <summary>
/// ViewModel pour le formulaire d'ajout / édition d'un employé.
/// </summary>
public class EmployeViewModel
{
    private readonly PaieDbContext _db;
    private string _idInterneText = "";

    public EmployeViewModel(PaieDbContext db, int? employeId = null)
    {
        _db = db;
        if (employeId.HasValue)
        {
            var existant = _db.Employes.FirstOrDefault(e => e.Id == employeId.Value);
            Employe = existant ?? new Employe();
        }
        else
            Employe = new Employe();

        _idInterneText = Employe.Id > 0 ? Employe.Id.ToString() : "Auto";
        IdsUtilisateursExistants = ConstruireListeIdsUtilisateursExistants();
        EmployesEnregistresResume = ConstruireResumeEmployesEnregistres();

        EnregistrerCommand = new RelayCommand(Enregistrer, _ => PeutEnregistrer);
        AnnulerCommand = new RelayCommand(Annuler);
        ChargerIdsTerminalCommand = new RelayCommand(_ => ChargerIdsTerminal());
        AttribuerIdTerminalSelectionneCommand = new RelayCommand(_ => AttribuerIdTerminalSelectionne(), _ => PeutEnregistrer);
    }

    public bool PeutEnregistrer => DroitsUi.PeutModifier;

    public bool FormulaireLectureSeule => !PeutEnregistrer;

    public Employe Employe { get; set; }

    /// <summary>True si on est en mode édition (employé existant).</summary>
    public bool EstModeEdition => Employe.Id > 0;

    public string TitreFenetre => EstModeEdition ? "Modifier l'employé" : "Nouvel employé";
    public string TitreFormulaire => EstModeEdition ? "Modifier l'employé" : "Ajouter un nouvel employé";

    /// <summary>
    /// Liste des départements pour le ComboBox (chargée au chargement de la fenêtre).
    /// </summary>
    public ObservableCollection<Departement> Departements { get; } = new();

    public ObservableCollection<string> Sexes { get; } = new() { "M", "F" };
    public ObservableCollection<string> EtatsCivils { get; } = new() { "Célibataire", "Marié(e)", "Divorcé(e)", "Veuf(ve)" };
    public ObservableCollection<ZktecoPointageReader.ZkUserDto> IdsTerminalDetectes { get; } = new();
    public ZktecoPointageReader.ZkUserDto? IdTerminalSelectionne { get; set; }
    public string IdsUtilisateursExistants { get; }
    public string EmployesEnregistresResume { get; }
    public string IdInterneText
    {
        get => _idInterneText;
        set => _idInterneText = value ?? "";
    }

    public ICommand EnregistrerCommand { get; }
    public ICommand AnnulerCommand { get; }
    public ICommand ChargerIdsTerminalCommand { get; }
    public ICommand AttribuerIdTerminalSelectionneCommand { get; }

    /// <summary>
    /// Charger les départements depuis la base (à appeler depuis la fenêtre).
    /// </summary>
    public void ChargerDepartements()
    {
        Departements.Clear();
        foreach (var d in ContexteEntrepriseService.DepartementsEntrepriseCourante(_db).OrderBy(x => x.NomDepartement))
            Departements.Add(d);
    }

    private void Enregistrer(object? _)
    {
        if (string.IsNullOrWhiteSpace(Employe.Matricule))
        {
            OnErreurValidation?.Invoke("Le matricule est obligatoire.");
            return;
        }
        if (string.IsNullOrWhiteSpace(Employe.Nom))
        {
            OnErreurValidation?.Invoke("Le nom est obligatoire.");
            return;
        }
        if (string.IsNullOrWhiteSpace(Employe.Postnom))
        {
            OnErreurValidation?.Invoke("Le postnom est obligatoire.");
            return;
        }
        if (string.IsNullOrWhiteSpace(Employe.Prenom))
        {
            OnErreurValidation?.Invoke("Le prénom est obligatoire.");
            return;
        }
        if (string.IsNullOrWhiteSpace(Employe.Sexe))
        {
            OnErreurValidation?.Invoke("Le sexe est obligatoire.");
            return;
        }
        if (string.IsNullOrWhiteSpace(Employe.EtatCivil))
        {
            OnErreurValidation?.Invoke("L'état civil est obligatoire.");
            return;
        }
        if (!Employe.DateNaissance.HasValue)
        {
            OnErreurValidation?.Invoke("La date de naissance est obligatoire.");
            return;
        }
        if (string.IsNullOrWhiteSpace(Employe.Telephone))
        {
            OnErreurValidation?.Invoke("Le téléphone est obligatoire.");
            return;
        }
        if (string.IsNullOrWhiteSpace(Employe.NumCnss))
        {
            OnErreurValidation?.Invoke("Le numéro CNSS est obligatoire.");
            return;
        }
        if (string.IsNullOrWhiteSpace(Employe.Adresse))
        {
            OnErreurValidation?.Invoke("L'adresse est obligatoire.");
            return;
        }
        if (Employe.DepartementId <= 0)
        {
            OnErreurValidation?.Invoke("Veuillez sélectionner un département.");
            return;
        }
        if (Employe.Id == 0)
        {
            if (_db.Employes.Any(e => e.Matricule == Employe.Matricule))
            {
                OnErreurValidation?.Invoke("Un employé avec ce matricule existe déjà.");
                return;
            }

            var entrepriseId = TenantDataBackfill.ResoudreEntrepriseIdDepuisDepartement(_db, Employe.DepartementId);
            if (entrepriseId <= 0)
                entrepriseId = ContexteEntrepriseService.ObtenirEntrepriseCouranteId(_db);
            Employe.EntrepriseId = entrepriseId;
            _db.Employes.Add(Employe);
        }
        else
        {
            var saisie = CapturerSaisieEmploye();
            if (!EssayerAppliquerChangementIdInterne())
                return;

            // Important EF: après permutation d'ID (clé primaire) via SQL, on purge le tracking
            // pour éviter « key cannot be modified » au SaveChanges.
            _db.ChangeTracker.Clear();
            var entite = _db.Employes.FirstOrDefault(x => x.Id == Employe.Id);
            if (entite == null)
            {
                OnErreurValidation?.Invoke("Employé introuvable après mise à jour de l'ID interne.");
                return;
            }

            AppliquerSaisieSurEntite(entite, saisie);
            Employe = entite;
        }

        AppliquerInterchangeIdUtilisateurSiNecessaire();
        _db.SaveChanges();
        OnEnregistreReussi?.Invoke();
    }

    private void AppliquerInterchangeIdUtilisateurSiNecessaire()
    {
        var nouvelId = NormaliserIdUtilisateur(Employe.ZkUserId);
        var ancienId = Employe.Id > 0
            ? NormaliserIdUtilisateur(_db.Employes.Where(x => x.Id == Employe.Id).Select(x => x.ZkUserId).FirstOrDefault())
            : "";

        Employe.ZkUserId = string.IsNullOrWhiteSpace(nouvelId) ? null : nouvelId;
        if (string.IsNullOrWhiteSpace(nouvelId))
            return;

        var autre = _db.Employes.FirstOrDefault(x => x.Id != Employe.Id && x.ZkUserId == nouvelId);
        if (autre == null)
            return;

        // Inter-échange : l'ancien ID utilisateur de l'employé courant est transféré à l'autre employé.
        autre.ZkUserId = string.IsNullOrWhiteSpace(ancienId) ? null : ancienId;
    }

    private bool EssayerAppliquerChangementIdInterne()
    {
        var texte = (IdInterneText ?? "").Trim();
        if (string.IsNullOrWhiteSpace(texte) || texte.Equals("Auto", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!int.TryParse(texte, out var nouvelId) || nouvelId <= 0)
        {
            OnErreurValidation?.Invoke("ID interne invalide (entier strictement positif).");
            return false;
        }

        var ancienId = Employe.Id;
        if (nouvelId == ancienId)
            return true;

        var cibleExiste = _db.Employes.Any(x => x.Id == nouvelId);
        _db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;");
        using var tx = _db.Database.BeginTransaction();
        try
        {
            if (cibleExiste)
            {
                // Inter-échange d'IDs : X->Y et Y->X, sans toucher aux matricules.
                var tempId = -ancienId - 1_000_000;
                DeplacerReferencesEmploye(ancienId, tempId);
                _db.Database.ExecuteSqlRaw("UPDATE Employes SET Id = {0} WHERE Id = {1}", tempId, ancienId);

                DeplacerReferencesEmploye(nouvelId, ancienId);
                _db.Database.ExecuteSqlRaw("UPDATE Employes SET Id = {0} WHERE Id = {1}", ancienId, nouvelId);

                DeplacerReferencesEmploye(tempId, nouvelId);
                _db.Database.ExecuteSqlRaw("UPDATE Employes SET Id = {0} WHERE Id = {1}", nouvelId, tempId);
            }
            else
            {
                DeplacerReferencesEmploye(ancienId, nouvelId);
                _db.Database.ExecuteSqlRaw("UPDATE Employes SET Id = {0} WHERE Id = {1}", nouvelId, ancienId);
            }

            tx.Commit();
            Employe.Id = nouvelId;
            IdInterneText = nouvelId.ToString();
            _db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
            _db.Database.ExecuteSqlRaw("PRAGMA foreign_key_check;");
            return true;
        }
        catch (Exception ex)
        {
            tx.Rollback();
            _db.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
            OnErreurValidation?.Invoke($"Impossible de modifier l'ID interne : {ex.Message}");
            return false;
        }
    }

    private void DeplacerReferencesEmploye(int ancienId, int nouvelId)
    {
        _db.Database.ExecuteSqlRaw("UPDATE Contrats SET EmployeId = {0} WHERE EmployeId = {1}", nouvelId, ancienId);
        _db.Database.ExecuteSqlRaw("UPDATE AyantsDroit SET EmployeId = {0} WHERE EmployeId = {1}", nouvelId, ancienId);
        _db.Database.ExecuteSqlRaw("UPDATE PretsAvances SET EmployeId = {0} WHERE EmployeId = {1}", nouvelId, ancienId);
        _db.Database.ExecuteSqlRaw("UPDATE AbsencesConges SET EmployeId = {0} WHERE EmployeId = {1}", nouvelId, ancienId);
        _db.Database.ExecuteSqlRaw("UPDATE AffectationsPrimesIndemnites SET EmployeId = {0} WHERE EmployeId = {1}", nouvelId, ancienId);
        _db.Database.ExecuteSqlRaw("UPDATE BulletinsPaie SET EmployeId = {0} WHERE EmployeId = {1}", nouvelId, ancienId);
        _db.Database.ExecuteSqlRaw("UPDATE SuivisJournaliers SET EmployeId = {0} WHERE EmployeId = {1}", nouvelId, ancienId);
        _db.Database.ExecuteSqlRaw("UPDATE SaisiesPaie SET EmployeId = {0} WHERE EmployeId = {1}", nouvelId, ancienId);
    }

    private string ConstruireListeIdsUtilisateursExistants()
    {
        var ids = _db.Employes
            .Where(x => x.Id != Employe.Id && !string.IsNullOrWhiteSpace(x.ZkUserId))
            .Select(x => x.ZkUserId!)
            .OrderBy(x => x)
            .ToList();
        return ids.Count == 0 ? "Aucun ID utilisateur déjà enregistré." : string.Join(", ", ids);
    }

    private void ChargerIdsTerminal()
    {
        try
        {
            ParametresApplicationHelper.EnsureRow(_db);
            var p = _db.ParametresApplication.FirstOrDefault(x => x.Id == ParametresApplication.SingletonId);
            if (p == null || string.IsNullOrWhiteSpace(p.ZkTerminalIp))
            {
                OnErreurValidation?.Invoke("Paramètres terminal manquants (IP/port). Configurez d'abord ZKTeco dans Paramètres.");
                return;
            }

            var ip = p.ZkTerminalIp.Trim();
            var port = p.ZkTerminalPort > 0 ? p.ZkTerminalPort : 4370;
            var machine = p.ZkMachineNumber > 0 ? p.ZkMachineNumber : 1;
            var users = ZktecoPointageReader.LireUtilisateurs(ip, port, machine, p.ZkCommPassword);

            IdsTerminalDetectes.Clear();
            foreach (var u in users.OrderBy(x => x.Id))
                IdsTerminalDetectes.Add(u);

            OnInfo?.Invoke($"{IdsTerminalDetectes.Count} ID(s) utilisateur lus depuis le terminal.");
        }
        catch (Exception ex)
        {
            OnErreurValidation?.Invoke(ex.Message);
        }
    }

    private void AttribuerIdTerminalSelectionne()
    {
        if (IdTerminalSelectionne == null || string.IsNullOrWhiteSpace(IdTerminalSelectionne.Id))
        {
            OnErreurValidation?.Invoke("Sélectionnez d'abord un ID terminal dans la liste.");
            return;
        }

        Employe.ZkUserId = IdTerminalSelectionne.Id.Trim();
        OnInfo?.Invoke($"ID terminal attribué à l'employé : {Employe.ZkUserId}");
    }

    private string ConstruireResumeEmployesEnregistres()
    {
        var lignes = _db.Employes
            .OrderBy(x => x.Id)
            .Select(x => $"{x.Id} | {x.Matricule} | {x.Nom} {x.Postnom} {x.Prenom}".Trim())
            .ToList();

        return lignes.Count == 0
            ? "Aucun employé enregistré."
            : string.Join(Environment.NewLine, lignes);
    }

    private static string NormaliserIdUtilisateur(string? valeur) => (valeur ?? "").Trim();

    private EmployeSaisieSnapshot CapturerSaisieEmploye() => new()
    {
        Matricule = Employe.Matricule,
        ZkUserId = Employe.ZkUserId,
        Nom = Employe.Nom,
        Postnom = Employe.Postnom,
        Prenom = Employe.Prenom,
        Sexe = Employe.Sexe,
        EtatCivil = Employe.EtatCivil,
        DateNaissance = Employe.DateNaissance,
        Telephone = Employe.Telephone,
        NumCnss = Employe.NumCnss,
        CommuneAffectation = Employe.CommuneAffectation,
        TypeTravailleurCnss = Employe.TypeTravailleurCnss,
        Adresse = Employe.Adresse,
        DepartementId = Employe.DepartementId
    };

    private static void AppliquerSaisieSurEntite(Employe cible, EmployeSaisieSnapshot saisie)
    {
        cible.Matricule = saisie.Matricule;
        cible.ZkUserId = saisie.ZkUserId;
        cible.Nom = saisie.Nom;
        cible.Postnom = saisie.Postnom;
        cible.Prenom = saisie.Prenom;
        cible.Sexe = saisie.Sexe;
        cible.EtatCivil = saisie.EtatCivil;
        cible.DateNaissance = saisie.DateNaissance;
        cible.Telephone = saisie.Telephone;
        cible.NumCnss = saisie.NumCnss;
        cible.CommuneAffectation = saisie.CommuneAffectation;
        cible.TypeTravailleurCnss = saisie.TypeTravailleurCnss is 1 or 2 ? saisie.TypeTravailleurCnss : 1;
        cible.Adresse = saisie.Adresse;
        cible.DepartementId = saisie.DepartementId;
    }

    private sealed class EmployeSaisieSnapshot
    {
        public string Matricule { get; set; } = "";
        public string? ZkUserId { get; set; }
        public string Nom { get; set; } = "";
        public string? Postnom { get; set; }
        public string? Prenom { get; set; }
        public string? Sexe { get; set; }
        public string? EtatCivil { get; set; }
        public DateTime? DateNaissance { get; set; }
        public string? Telephone { get; set; }
        public string? NumCnss { get; set; }
        public string? CommuneAffectation { get; set; }
        public int TypeTravailleurCnss { get; set; } = 1;
        public string? Adresse { get; set; }
        public int DepartementId { get; set; }
    }

    /// <summary>
    /// Message d'erreur de validation (afficher dans un MessageBox).
    /// </summary>
    public Action<string>? OnErreurValidation { get; set; }
    public Action<string>? OnInfo { get; set; }

    private void Annuler(object? _) => OnAnnuler?.Invoke();

    /// <summary>
    /// Appelé après enregistrement réussi (pour fermer la fenêtre).
    /// </summary>
    public Action? OnEnregistreReussi { get; set; }

    /// <summary>
    /// Appelé sur annulation (pour fermer la fenêtre).
    /// </summary>
    public Action? OnAnnuler { get; set; }
}

/// <summary>
/// Commande simple pour les boutons.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
