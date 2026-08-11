using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.ViewModels;

public sealed class EmployeFicheViewModel : INotifyPropertyChanged
{
    private static readonly CultureInfo Fr = new("fr-FR");
    private readonly PaieDbContext _db;
    private int _employeId;

    private string _matricule = "";
    private string _nomComplet = "";
    private string _sexe = "—";
    private string _etatCivil = "—";
    private string _dateNaissance = "—";
    private string _telephone = "—";
    private string _adresse = "—";
    private string _departement = "—";
    private string _zkUserId = "—";
    private string _numCnss = "—";
    private string _commune = "—";
    private string _typeTravailleur = "—";
    private string _banque = "—";
    private string _compte = "—";
    private string _salaireUsd = "—";
    private string _salaireCdf = "—";
    private string _salaireJourUsd = "—";
    private string _salaireHeureUsd = "—";
    private string _contratActif = "—";
    private string _nbAyantsDroit = "0";
    private string _nbPretsActifs = "0";
    private string _nbAbsences = "0";

    public EmployeFicheViewModel(PaieDbContext db) => _db = db;

    public bool AfficherFiche => _employeId > 0;

    public string MessageVide =>
        !AfficherFiche
            ? "Sélectionnez un employé dans le répertoire ou double-cliquez sur une ligne pour afficher sa fiche."
            : "";

    public string Matricule { get => _matricule; private set { _matricule = value; OnPropertyChanged(); } }
    public string NomComplet { get => _nomComplet; private set { _nomComplet = value; OnPropertyChanged(); } }
    public string Sexe { get => _sexe; private set { _sexe = value; OnPropertyChanged(); } }
    public string EtatCivil { get => _etatCivil; private set { _etatCivil = value; OnPropertyChanged(); } }
    public string DateNaissance { get => _dateNaissance; private set { _dateNaissance = value; OnPropertyChanged(); } }
    public string Telephone { get => _telephone; private set { _telephone = value; OnPropertyChanged(); } }
    public string Adresse { get => _adresse; private set { _adresse = value; OnPropertyChanged(); } }
    public string Departement { get => _departement; private set { _departement = value; OnPropertyChanged(); } }
    public string ZkUserId { get => _zkUserId; private set { _zkUserId = value; OnPropertyChanged(); } }
    public string NumCnss { get => _numCnss; private set { _numCnss = value; OnPropertyChanged(); } }
    public string Commune { get => _commune; private set { _commune = value; OnPropertyChanged(); } }
    public string TypeTravailleur { get => _typeTravailleur; private set { _typeTravailleur = value; OnPropertyChanged(); } }
    public string Banque { get => _banque; private set { _banque = value; OnPropertyChanged(); } }
    public string Compte { get => _compte; private set { _compte = value; OnPropertyChanged(); } }
    public string SalaireUsd { get => _salaireUsd; private set { _salaireUsd = value; OnPropertyChanged(); } }
    public string SalaireCdf { get => _salaireCdf; private set { _salaireCdf = value; OnPropertyChanged(); } }
    public string SalaireJourUsd { get => _salaireJourUsd; private set { _salaireJourUsd = value; OnPropertyChanged(); } }
    public string SalaireHeureUsd { get => _salaireHeureUsd; private set { _salaireHeureUsd = value; OnPropertyChanged(); } }
    public string ContratActif { get => _contratActif; private set { _contratActif = value; OnPropertyChanged(); } }
    public string NbAyantsDroit { get => _nbAyantsDroit; private set { _nbAyantsDroit = value; OnPropertyChanged(); } }
    public string NbPretsActifs { get => _nbPretsActifs; private set { _nbPretsActifs = value; OnPropertyChanged(); } }
    public string NbAbsences { get => _nbAbsences; private set { _nbAbsences = value; OnPropertyChanged(); } }

    public void ChargerPourEmploye(int? employeId, Employe? employeListe = null)
    {
        _employeId = employeId.GetValueOrDefault();
        if (_employeId <= 0)
        {
            Reinitialiser();
            OnPropertyChanged(nameof(AfficherFiche));
            OnPropertyChanged(nameof(MessageVide));
            return;
        }

        var e = _db.Employes.AsNoTracking()
                     .Include(x => x.Departement)
                     .FirstOrDefault(x => x.Id == _employeId)
                 ?? employeListe;

        if (e == null)
        {
            Reinitialiser();
            OnPropertyChanged(nameof(AfficherFiche));
            OnPropertyChanged(nameof(MessageVide));
            return;
        }

        if (employeListe != null)
        {
            e.SalaireMensuelUsd = employeListe.SalaireMensuelUsd;
            e.SalaireMensuelCdf = employeListe.SalaireMensuelCdf;
            e.JoursReferencePaie = employeListe.JoursReferencePaie;
            e.HeuresParJour = employeListe.HeuresParJour;
        }

        Matricule = Valeur(e.Matricule);
        NomComplet = e.NomComplet;
        Sexe = Valeur(e.Sexe);
        EtatCivil = Valeur(e.EtatCivil);
        DateNaissance = e.DateNaissance.HasValue
            ? e.DateNaissance.Value.ToString("dd MMMM yyyy", Fr)
            : "—";
        Telephone = Valeur(e.Telephone);
        Adresse = Valeur(e.Adresse);
        Departement = Valeur(e.Departement?.NomDepartement);
        ZkUserId = Valeur(e.ZkUserId);
        NumCnss = Valeur(e.NumCnss);
        Commune = Valeur(e.CommuneAffectation);
        TypeTravailleur = e.TypeTravailleurCnss == 2 ? "Assimilé (2)" : "Travailleur (1)";

        var banque = string.Join(" — ", new[] { e.LibelleBanque, e.CodeBanque }.Where(s => !string.IsNullOrWhiteSpace(s)));
        Banque = string.IsNullOrWhiteSpace(banque) ? "—" : banque;
        Compte = Valeur(e.NumeroCompteBancaire);
        if (!string.IsNullOrWhiteSpace(e.TitulaireCompteBancaire))
            Compte = $"{Compte} ({e.TitulaireCompteBancaire})";

        SalaireUsd = e.SalaireMensuelUsd > 0 ? $"{e.SalaireMensuelUsd:N2} USD" : "—";
        SalaireCdf = e.SalaireMensuelCdf > 0 ? $"{e.SalaireMensuelCdf:N0} FC" : "—";
        SalaireJourUsd = e.SalaireJourUsd > 0 ? $"{e.SalaireJourUsd:N2} USD" : "—";
        SalaireHeureUsd = e.SalaireHeureUsd > 0 ? $"{e.SalaireHeureUsd:N2} USD" : "—";

        var contrat = _db.Contrats.AsNoTracking()
            .Where(c => c.EmployeId == _employeId)
            .OrderByDescending(c => c.DateDebut)
            .FirstOrDefault();
        ContratActif = contrat == null
            ? "Aucun contrat"
            : $"{contrat.TypeContrat ?? "Contrat"} — {contrat.DateDebut:dd/MM/yyyy}" +
              (contrat.DateFin.HasValue ? $" → {contrat.DateFin:dd/MM/yyyy}" : " (en cours)");

        NbAyantsDroit = _db.AyantsDroit.Count(a => a.EmployeId == _employeId).ToString(Fr);
        NbPretsActifs = _db.PretsAvances.Count(p => p.EmployeId == _employeId && p.SoldeRestant > 0).ToString(Fr);
        NbAbsences = _db.AbsencesConges.Count(a => a.EmployeId == _employeId).ToString(Fr);

        OnPropertyChanged(nameof(AfficherFiche));
        OnPropertyChanged(nameof(MessageVide));
    }

    private static string Valeur(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s.Trim();

    private void Reinitialiser()
    {
        Matricule = NomComplet = Sexe = EtatCivil = DateNaissance = Telephone = Adresse = Departement = "";
        ZkUserId = NumCnss = Commune = TypeTravailleur = Banque = Compte = "—";
        SalaireUsd = SalaireCdf = SalaireJourUsd = SalaireHeureUsd = ContratActif = "—";
        NbAyantsDroit = NbPretsActifs = NbAbsences = "0";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
