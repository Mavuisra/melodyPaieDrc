using System.IO;
using System.Text.Json;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Forms.Metadata;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Charge les définitions de formulaires depuis AppData/Forms (JSON).
/// Copie les modèles par défaut au premier lancement — aucune recompilation requise pour modifier un écran.
/// </summary>
public static class FormDefinitionLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static void AssurerDossierEtModelesParDefaut()
    {
        var formsDir = PaieDbContext.GetFormsDirectory();
        if (!Directory.Exists(formsDir))
            Directory.CreateDirectory(formsDir);

        var defaultsDir = Path.Combine(AppContext.BaseDirectory, "Forms", "Defaults");
        if (!Directory.Exists(defaultsDir))
            return;

        foreach (var source in Directory.EnumerateFiles(defaultsDir, "*.json"))
        {
            var dest = Path.Combine(formsDir, Path.GetFileName(source));
            if (!File.Exists(dest))
                File.Copy(source, dest);
        }
    }

    public static IReadOnlyList<FormDefinitionSummary> ListerFormulaires()
    {
        AssurerDossierEtModelesParDefaut();
        var formsDir = PaieDbContext.GetFormsDirectory();
        var list = new List<FormDefinitionSummary>();

        foreach (var path in Directory.EnumerateFiles(formsDir, "*.json").OrderBy(p => p))
        {
            try
            {
                var def = ChargerDepuisFichier(path);
                if (def == null || string.IsNullOrWhiteSpace(def.FormId)) continue;
                list.Add(new FormDefinitionSummary
                {
                    FormId = def.FormId,
                    Title = string.IsNullOrWhiteSpace(def.Title) ? def.FormId : def.Title,
                    EntityType = def.EntityType,
                    FilePath = path,
                    Version = def.Version
                });
            }
            catch
            {
                // Fichier JSON invalide : ignoré dans la liste
            }
        }

        return list;
    }

    public static FormDefinition? ChargerParId(string formId)
    {
        AssurerDossierEtModelesParDefaut();
        var path = Path.Combine(PaieDbContext.GetFormsDirectory(), $"{formId}.json");
        if (!File.Exists(path))
        {
            var match = Directory.EnumerateFiles(PaieDbContext.GetFormsDirectory(), "*.json")
                .FirstOrDefault(f =>
                {
                    try
                    {
                        var d = ChargerDepuisFichier(f);
                        return d != null && string.Equals(d.FormId, formId, StringComparison.OrdinalIgnoreCase);
                    }
                    catch { return false; }
                });
            if (match == null) return null;
            path = match;
        }

        return ChargerDepuisFichier(path);
    }

    public static FormDefinition? ChargerDepuisFichier(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<FormDefinition>(json, JsonOptions);
    }

    public static void RechargerFormulaire(string formId, out FormDefinition? definition, out string? erreur)
    {
        erreur = null;
        definition = null;
        try
        {
            definition = ChargerParId(formId);
            if (definition == null)
                erreur = $"Formulaire « {formId} » introuvable dans {PaieDbContext.GetFormsDirectory()}.";
        }
        catch (Exception ex)
        {
            erreur = $"Erreur de lecture du JSON : {ex.Message}";
        }
    }
}

public sealed class FormDefinitionSummary
{
    public string FormId { get; init; } = "";
    public string Title { get; init; } = "";
    public string EntityType { get; init; } = "";
    public string FilePath { get; init; } = "";
    public string Version { get; init; } = "";
}
