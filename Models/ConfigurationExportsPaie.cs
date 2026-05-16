namespace MelodyPaieRDC.Models;

/// <summary>
/// Configuration des exports paie, livre réglementaire, virements et clôture (JSON par entreprise).
/// </summary>
public class ConfigurationExportsPaie
{
    public ProfilExportConfig ExportCnssEdeclaration { get; set; } = new();
    public ProfilExportConfig ExportIprDgi { get; set; } = new();
    public ProfilExportConfig LivrePaieReglementaire { get; set; } = new();
    public ProfilExportConfig BulletinReglementaire { get; set; } = new();
    public List<ProfilBanqueVirement> ProfilsVirement { get; set; } = new();
    public CloturePaieConfig Cloture { get; set; } = new();
    /// <summary>Code du profil bancaire utilisé par défaut pour les virements.</summary>
    public string CodeProfilVirementParDefaut { get; set; } = "GENERIQUE_CSV";
}

public class ProfilExportConfig
{
    public string Code { get; set; } = "";
    public string Libelle { get; set; } = "";
    /// <summary>Csv, Excel, TexteFixe</summary>
    public string TypeFormat { get; set; } = "Csv";
    public string Separateur { get; set; } = ";";
    public string ExtensionFichier { get; set; } = "csv";
    public bool InclureBomUtf8 { get; set; } = true;
    public bool InclureLignesEnteteEmployeur { get; set; } = true;
    public List<LigneEnteteExport> LignesEnteteEmployeur { get; set; } = new();
    public List<ColonneExportConfig> Colonnes { get; set; } = new();
}

public class LigneEnteteExport
{
    public string Libelle { get; set; } = "";
    public string SourceDonnee { get; set; } = "";
    public string? ValeurFixe { get; set; }
}

public class ColonneExportConfig
{
    public string Code { get; set; } = "";
    public string Libelle { get; set; } = "";
    /// <summary>Jeton résolu par ExportDonneesPaieResolver (ex. Employe.NumCnss, Bulletin.NetAPayer).</summary>
    public string SourceDonnee { get; set; } = "";
    public bool Actif { get; set; } = true;
    public int Ordre { get; set; }
    public string? FormatNombre { get; set; }
    public int? LargeurFixe { get; set; }
    /// <summary>Gauche ou Droite (fichiers à largeur fixe).</summary>
    public string? AlignementFixe { get; set; }
    public string? ValeurParDefaut { get; set; }
}

public class ProfilBanqueVirement
{
    public string Code { get; set; } = "";
    public string Libelle { get; set; } = "";
    public string TypeFormat { get; set; } = "Csv";
    public string Separateur { get; set; } = ";";
    public string ExtensionFichier { get; set; } = "csv";
    public string? PrefixeReference { get; set; }
    public List<ColonneExportConfig> Colonnes { get; set; } = new();
}

public class CloturePaieConfig
{
    public bool ExigerControlesSansErreur { get; set; } = true;
    public bool ExportsOfficielsExigentPeriodeCloturee { get; set; } = false;
    public bool BloquerSaisiePaieSiCloturee { get; set; } = true;
    public bool BloquerSuppressionBulletinSiCloturee { get; set; } = true;
    public List<ControleClotureConfig> Controles { get; set; } = new();
}

public class ControleClotureConfig
{
    public string Code { get; set; } = "";
    public string Libelle { get; set; } = "";
  /// <summary>Erreur ou Avertissement</summary>
    public string Severite { get; set; } = "Erreur";
    public bool Actif { get; set; } = true;
    /// <summary>Paramètres libres (ex. seuil, codes banque obligatoires).</summary>
    public Dictionary<string, string> Parametres { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
