using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.ViewModels;

public sealed class ColonneExportItem : INotifyPropertyChanged
{
    public string Code { get; init; } = "";
    public string SourceDonnee { get; init; } = "";
    private string _libelle = "";
    private bool _actif = true;
    private int _ordre;

    public string Libelle
    {
        get => _libelle;
        set { _libelle = value; OnPropertyChanged(); }
    }

    public bool Actif
    {
        get => _actif;
        set { _actif = value; OnPropertyChanged(); }
    }

    public int Ordre
    {
        get => _ordre;
        set { _ordre = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class ControleClotureItem : INotifyPropertyChanged
{
    public string Code { get; init; } = "";
    private string _libelle = "";
    private string _severite = "Erreur";
    private bool _actif = true;

    public string Libelle
    {
        get => _libelle;
        set { _libelle = value; OnPropertyChanged(); }
    }

    public string Severite
    {
        get => _severite;
        set { _severite = value; OnPropertyChanged(); }
    }

    public bool Actif
    {
        get => _actif;
        set { _actif = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class ConfigurationExportsPaieViewModel : INotifyPropertyChanged
{
    private readonly PaieDbContext _db;
    private ConfigurationExportsPaie _config = new();
    private string _profilSelectionne = "CNSS";

    public ConfigurationExportsPaieViewModel(PaieDbContext db)
    {
        _db = db;
        Colonnes = new ObservableCollection<ColonneExportItem>();
        ControlesCloture = new ObservableCollection<ControleClotureItem>();
        ProfilsVirement = new ObservableCollection<string>();

        EnregistrerCommand = new RelayCommand(_ => Enregistrer(), _ => DroitsUi.PeutModifier);
        ReinitialiserCommand = new RelayCommand(_ => Reinitialiser(), _ => DroitsUi.PeutModifier);
        ChargerColonnesProfilCommand = new RelayCommand(_ => ChargerColonnesProfil());

        Charger();
    }

    public ObservableCollection<ColonneExportItem> Colonnes { get; }
    public ObservableCollection<ControleClotureItem> ControlesCloture { get; }
    public ObservableCollection<string> ProfilsVirement { get; }

    public string[] ProfilsDisponibles { get; } = ["CNSS", "IPR", "LIVRE", "VIREMENT"];

    public string ProfilSelectionne
    {
        get => _profilSelectionne;
        set { _profilSelectionne = value; OnPropertyChanged(); ChargerColonnesProfil(); }
    }

    public bool ExigerControlesSansErreur
    {
        get => _config.Cloture.ExigerControlesSansErreur;
        set { _config.Cloture.ExigerControlesSansErreur = value; OnPropertyChanged(); }
    }

    public bool ExportsExigentCloture
    {
        get => _config.Cloture.ExportsOfficielsExigentPeriodeCloturee;
        set { _config.Cloture.ExportsOfficielsExigentPeriodeCloturee = value; OnPropertyChanged(); }
    }

    public bool BloquerSaisieSiCloturee
    {
        get => _config.Cloture.BloquerSaisiePaieSiCloturee;
        set { _config.Cloture.BloquerSaisiePaieSiCloturee = value; OnPropertyChanged(); }
    }

    public string CodeProfilVirementParDefaut
    {
        get => _config.CodeProfilVirementParDefaut;
        set { _config.CodeProfilVirementParDefaut = value; OnPropertyChanged(); }
    }

    public ICommand EnregistrerCommand { get; }
    public ICommand ReinitialiserCommand { get; }
    public ICommand ChargerColonnesProfilCommand { get; }

    public bool PeutModifier => DroitsUi.PeutModifier;

    public Action? OnEnregistre { get; set; }

    private void Charger()
    {
        _config = ConfigurationExportsPaieService.Obtenir(_db);
        ProfilsVirement.Clear();
        foreach (var p in _config.ProfilsVirement)
            ProfilsVirement.Add($"{p.Code} — {p.Libelle}");

        ControlesCloture.Clear();
        foreach (var c in _config.Cloture.Controles)
        {
            ControlesCloture.Add(new ControleClotureItem
            {
                Code = c.Code,
                Libelle = c.Libelle,
                Severite = c.Severite,
                Actif = c.Actif
            });
        }

        OnPropertyChanged(nameof(ExigerControlesSansErreur));
        OnPropertyChanged(nameof(ExportsExigentCloture));
        OnPropertyChanged(nameof(BloquerSaisieSiCloturee));
        OnPropertyChanged(nameof(CodeProfilVirementParDefaut));
        ChargerColonnesProfil();
    }

    private void ChargerColonnesProfil()
    {
        Colonnes.Clear();
        List<ColonneExportConfig> colonnesSource = ProfilSelectionne switch
        {
            "IPR" => _config.ExportIprDgi.Colonnes,
            "LIVRE" => _config.LivrePaieReglementaire.Colonnes,
            "VIREMENT" => _config.ProfilsVirement
                .FirstOrDefault(p => string.Equals(p.Code, ExtraireCodeProfilVirement(CodeProfilVirementParDefaut), StringComparison.OrdinalIgnoreCase))
                ?.Colonnes ?? _config.ProfilsVirement.FirstOrDefault()?.Colonnes ?? new List<ColonneExportConfig>(),
            _ => _config.ExportCnssEdeclaration.Colonnes
        };

        foreach (var c in colonnesSource.OrderBy(x => x.Ordre))
        {
            Colonnes.Add(new ColonneExportItem
            {
                Code = c.Code,
                Libelle = c.Libelle,
                SourceDonnee = c.SourceDonnee,
                Actif = c.Actif,
                Ordre = c.Ordre
            });
        }
    }

    private static string ExtraireCodeProfilVirement(string texte)
    {
        var idx = texte.IndexOf('—');
        return idx > 0 ? texte[..idx].Trim() : texte.Trim();
    }

    private void Enregistrer()
    {
        AppliquerColonnesVersProfil();
        foreach (var item in ControlesCloture)
        {
            var c = _config.Cloture.Controles.FirstOrDefault(x =>
                string.Equals(x.Code, item.Code, StringComparison.OrdinalIgnoreCase));
            if (c is null) continue;
            c.Libelle = item.Libelle;
            c.Severite = item.Severite;
            c.Actif = item.Actif;
        }
        ConfigurationExportsPaieService.Enregistrer(_db, _config);
        OnEnregistre?.Invoke();
    }

    private void AppliquerColonnesVersProfil()
    {
        var liste = Colonnes.Select(c => new ColonneExportConfig
        {
            Code = c.Code,
            Libelle = c.Libelle,
            SourceDonnee = c.SourceDonnee,
            Actif = c.Actif,
            Ordre = c.Ordre
        }).ToList();

        switch (ProfilSelectionne)
        {
            case "IPR":
                _config.ExportIprDgi.Colonnes = liste;
                break;
            case "LIVRE":
                _config.LivrePaieReglementaire.Colonnes = liste;
                _config.BulletinReglementaire.Colonnes = liste;
                break;
            case "VIREMENT":
                var code = ExtraireCodeProfilVirement(CodeProfilVirementParDefaut);
                var profil = _config.ProfilsVirement.FirstOrDefault(p =>
                    string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));
                if (profil != null) profil.Colonnes = liste;
                break;
            default:
                _config.ExportCnssEdeclaration.Colonnes = liste;
                break;
        }
    }

    private void Reinitialiser()
    {
        _config = ConfigurationExportsPaieDefaults.Creer();
        Charger();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? n = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
