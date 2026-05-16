namespace MelodyPaieRDC.Models;

/// <summary>
/// Configuration UI et modules par entreprise (sérialisée en JSON, sans recompilation).
/// </summary>
public class ConfigurationPlateforme
{
    public Dictionary<string, bool> ModulesActifs { get; set; } = CreerModulesParDefaut();

    public Dictionary<string, bool> ColonnesListeEmployes { get; set; } = CreerColonnesEmployesParDefaut();

    /// <summary>Exports CNSS, DGI, livre réglementaire, virements, clôture.</summary>
    public ConfigurationExportsPaie ExportsPaie { get; set; } = new();

    public static Dictionary<string, bool> CreerModulesParDefaut() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["TableauBord"] = true,
        ["Pointage"] = true,
        ["Heures"] = true,
        ["Employes"] = true,
        ["Paie"] = true,
        ["Declarations"] = true,
        ["Bulletins"] = true,
        ["Rapports"] = true,
        ["Parametres"] = true
    };

    public static Dictionary<string, bool> CreerColonnesEmployesParDefaut() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["Matricule"] = true,
        ["Nom"] = true,
        ["Postnom"] = true,
        ["Prenom"] = true,
        ["Sexe"] = true,
        ["Telephone"] = true,
        ["Departement"] = true,
        ["SalaireMensuelUsd"] = true,
        ["SalaireMensuelCdf"] = true,
        ["SalaireJourUsd"] = false,
        ["SalaireJourCdf"] = false,
        ["SalaireHeureUsd"] = false,
        ["SalaireHeureCdf"] = false
    };
}
