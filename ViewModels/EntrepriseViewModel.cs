using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.ViewModels;

public class EntrepriseViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private int _id;
    private string _raisonSociale = string.Empty;
    private string? _nif;
    private string? _nrc;
    private string? _idNat;
    private string? _numCnss;
    private string? _numInpp;
    private string? _adresse;
    private string? _telephone;
    private string? _email;
    private string? _siteWeb;
    private string? _numeroAffiliationCnss;
    private string? _logo;
    private string? _couleurPrincipale;
    private string? _couleurSecondaire;

    public EntrepriseViewModel(PaieDbContext db)
    {
        _db = db;
        EnregistrerCommand = new RelayCommand(_ => Enregistrer());
        ChoisirLogoCommand = new RelayCommand(_ => ChoisirLogo());
        SupprimerLogoCommand = new RelayCommand(_ => SupprimerLogo(), _ => !string.IsNullOrEmpty(Logo));
    }

    public int Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string RaisonSociale { get => _raisonSociale; set { _raisonSociale = value ?? string.Empty; OnPropertyChanged(); } }
    public string? Nif { get => _nif; set { _nif = value; OnPropertyChanged(); } }
    public string? Nrc { get => _nrc; set { _nrc = value; OnPropertyChanged(); } }
    public string? IdNat { get => _idNat; set { _idNat = value; OnPropertyChanged(); } }
    public string? NumCnss { get => _numCnss; set { _numCnss = value; OnPropertyChanged(); } }
    public string? NumInpp { get => _numInpp; set { _numInpp = value; OnPropertyChanged(); } }
    public string? Adresse { get => _adresse; set { _adresse = value; OnPropertyChanged(); } }
    public string? Telephone { get => _telephone; set { _telephone = value; OnPropertyChanged(); } }
    public string? Email { get => _email; set { _email = value; OnPropertyChanged(); } }
    public string? SiteWeb { get => _siteWeb; set { _siteWeb = value; OnPropertyChanged(); } }
    public string? NumeroAffiliationCnss { get => _numeroAffiliationCnss; set { _numeroAffiliationCnss = value; OnPropertyChanged(); } }
    public string? Logo { get => _logo; set { _logo = value; OnPropertyChanged(); OnPropertyChanged(nameof(CheminLogoComplet)); (SupprimerLogoCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }
    public string? CouleurPrincipale { get => _couleurPrincipale; set { _couleurPrincipale = value; OnPropertyChanged(); } }
    public string? CouleurSecondaire { get => _couleurSecondaire; set { _couleurSecondaire = value; OnPropertyChanged(); } }

    /// <summary>Chemin complet du fichier logo pour affichage (dossier Data de l'app + nom fichier).</summary>
    public string? CheminLogoComplet
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Logo)) return null;
            var dataDir = PaieDbContext.GetDataDirectory();
            return Path.Combine(dataDir, Path.GetFileName(Logo));
        }
    }

    public ICommand EnregistrerCommand { get; }
    public ICommand ChoisirLogoCommand { get; }
    public ICommand SupprimerLogoCommand { get; }

    /// <summary>La vue l'assigne pour ouvrir OpenFileDialog ; si un fichier est choisi, elle appelle DefinirLogoDepuisFichier(chemin).</summary>
    public Action? OnDemandeChoisirLogo { get; set; }

    public Action<string>? OnErreur { get; set; }

    public void Charger()
    {
        var ent = _db.Entreprises.FirstOrDefault();
        if (ent != null)
        {
            Id = ent.Id;
            RaisonSociale = ent.RaisonSociale;
            Nif = ent.Nif;
            Nrc = ent.Nrc;
            IdNat = ent.IdNat;
            NumCnss = ent.NumCnss;
            NumInpp = ent.NumInpp;
            Adresse = ent.Adresse;
            Telephone = ent.Telephone;
            Email = ent.Email;
            SiteWeb = ent.SiteWeb;
            NumeroAffiliationCnss = ent.NumeroAffiliationCnss;
            Logo = ent.Logo;
            CouleurPrincipale = string.IsNullOrWhiteSpace(ent.CouleurPrincipale) ? "#1E3A5F" : ent.CouleurPrincipale;
            CouleurSecondaire = ent.CouleurSecondaire;
        }
        else
        {
            Id = 0;
            RaisonSociale = "Mon entreprise";
            Nif = null;
            Nrc = null;
            IdNat = null;
            NumCnss = null;
            NumInpp = null;
            Adresse = null;
            Telephone = null;
            Email = null;
            SiteWeb = null;
            NumeroAffiliationCnss = null;
            Logo = null;
            CouleurPrincipale = "#1E3A5F";
            CouleurSecondaire = null;
        }
    }

    /// <summary>Copie le fichier sélectionné vers Data/ et assigne Logo (nom du fichier).</summary>
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
            OnPropertyChanged(nameof(CheminLogoComplet));
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke($"Impossible de copier le logo : {ex.Message}");
        }
    }

    private void ChoisirLogo()
    {
        OnDemandeChoisirLogo?.Invoke();
    }

    private void SupprimerLogo()
    {
        Logo = null;
        var dataDir = PaieDbContext.GetDataDirectory();
        foreach (var f in new[] { "Logo.png", "Logo.jpg", "Logo.jpeg", "Logo.bmp", "Logo.gif" })
        {
            var p = Path.Combine(dataDir, f);
            if (File.Exists(p)) try { File.Delete(p); } catch { /* ignore */ }
        }
    }

    private void Enregistrer()
    {
        if (string.IsNullOrWhiteSpace(RaisonSociale))
        {
            OnErreur?.Invoke("La raison sociale est obligatoire.");
            return;
        }

        try
        {
            var ent = _db.Entreprises.Find(Id);
            if (ent != null)
            {
                ent.RaisonSociale = RaisonSociale.Trim();
                ent.Nif = string.IsNullOrWhiteSpace(Nif) ? null : Nif.Trim();
                ent.Nrc = string.IsNullOrWhiteSpace(Nrc) ? null : Nrc.Trim();
                ent.IdNat = string.IsNullOrWhiteSpace(IdNat) ? null : IdNat.Trim();
                ent.NumCnss = string.IsNullOrWhiteSpace(NumCnss) ? null : NumCnss.Trim();
                ent.NumInpp = string.IsNullOrWhiteSpace(NumInpp) ? null : NumInpp.Trim();
                ent.Adresse = string.IsNullOrWhiteSpace(Adresse) ? null : Adresse.Trim();
                ent.Telephone = string.IsNullOrWhiteSpace(Telephone) ? null : Telephone.Trim();
                ent.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
                ent.SiteWeb = string.IsNullOrWhiteSpace(SiteWeb) ? null : SiteWeb.Trim();
                ent.NumeroAffiliationCnss = string.IsNullOrWhiteSpace(NumeroAffiliationCnss) ? null : NumeroAffiliationCnss.Trim();
                ent.Logo = string.IsNullOrWhiteSpace(Logo) ? null : Logo.Trim();
                ent.CouleurPrincipale = NormaliserCouleurHex(CouleurPrincipale) ?? "#1E3A5F";
                ent.CouleurSecondaire = NormaliserCouleurHex(CouleurSecondaire);
            }
            else
            {
                _db.Entreprises.Add(new Entreprise
                {
                    RaisonSociale = RaisonSociale.Trim(),
                    Nif = string.IsNullOrWhiteSpace(Nif) ? null : Nif.Trim(),
                    Nrc = string.IsNullOrWhiteSpace(Nrc) ? null : Nrc.Trim(),
                    IdNat = string.IsNullOrWhiteSpace(IdNat) ? null : IdNat.Trim(),
                    NumCnss = string.IsNullOrWhiteSpace(NumCnss) ? null : NumCnss.Trim(),
                    NumInpp = string.IsNullOrWhiteSpace(NumInpp) ? null : NumInpp.Trim(),
                    Adresse = string.IsNullOrWhiteSpace(Adresse) ? null : Adresse.Trim(),
                    Telephone = string.IsNullOrWhiteSpace(Telephone) ? null : Telephone.Trim(),
                    Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
                    SiteWeb = string.IsNullOrWhiteSpace(SiteWeb) ? null : SiteWeb.Trim(),
                    NumeroAffiliationCnss = string.IsNullOrWhiteSpace(NumeroAffiliationCnss) ? null : NumeroAffiliationCnss.Trim(),
                    Logo = string.IsNullOrWhiteSpace(Logo) ? null : Logo.Trim(),
                    CouleurPrincipale = NormaliserCouleurHex(CouleurPrincipale) ?? "#1E3A5F",
                    CouleurSecondaire = NormaliserCouleurHex(CouleurSecondaire)
                });
            }

            _db.SaveChanges();
            var first = _db.Entreprises.OrderBy(e => e.Id).FirstOrDefault();
            if (first != null) Id = first.Id;
            OnEnregistre?.Invoke();
        }
        catch (Exception ex)
        {
            OnErreur?.Invoke(ex.Message);
        }
    }

    public Action? OnEnregistre { get; set; }

    private static string? NormaliserCouleurHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var s = value.Trim();
        if (s.StartsWith("#")) return s.Length >= 4 && s.Length <= 9 ? s : null;
        if (s.Length >= 3 && s.Length <= 8) return "#" + s;
        return null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
