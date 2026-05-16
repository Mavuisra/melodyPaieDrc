using System.Text.Json.Serialization;

namespace MelodyPaieRDC.Forms.Metadata;

/// <summary>
/// Définition complète d'un écran de formulaire (fichier JSON dans AppData/Forms).
/// Modifiable sans recompilation de l'application.
/// </summary>
public sealed class FormDefinition
{
    [JsonPropertyName("formId")]
    public string FormId { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Type d'entité liée (Employe, Entreprise, Contrat, …).</summary>
    [JsonPropertyName("entityType")]
    public string EntityType { get; set; } = "";

    [JsonPropertyName("width")]
    public int Width { get; set; } = 480;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 520;

    [JsonPropertyName("sections")]
    public List<FormSection> Sections { get; set; } = new();

    [JsonPropertyName("actions")]
    public List<FormAction> Actions { get; set; } = new();
}

public sealed class FormSection
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("fields")]
    public List<FieldDefinition> Fields { get; set; } = new();
}

public sealed class FieldDefinition
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "text";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("hint")]
    public string? Hint { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("readOnly")]
    public bool ReadOnly { get; set; }

    [JsonPropertyName("maxLength")]
    public int? MaxLength { get; set; }

    [JsonPropertyName("min")]
    public decimal? Min { get; set; }

    [JsonPropertyName("max")]
    public decimal? Max { get; set; }

    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; set; }

    [JsonPropertyName("choices")]
    public List<string>? Choices { get; set; }

    [JsonPropertyName("lookup")]
    public LookupDefinition? Lookup { get; set; }

    [JsonPropertyName("visibleWhen")]
    public VisibilityCondition? VisibleWhen { get; set; }
}

public sealed class LookupDefinition
{
    /// <summary>Source : static, departements, categoriesProfessionnelles, periodesPaie.</summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = "static";

    [JsonPropertyName("valueField")]
    public string ValueField { get; set; } = "Id";

    [JsonPropertyName("displayField")]
    public string DisplayField { get; set; } = "Nom";

    [JsonPropertyName("items")]
    public List<LookupItem>? Items { get; set; }
}

public sealed class LookupItem
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";
}

public sealed class VisibilityCondition
{
    [JsonPropertyName("field")]
    public string Field { get; set; } = "";

    [JsonPropertyName("equals")]
    public string? EqualsValue { get; set; }

    [JsonPropertyName("notEquals")]
    public string? NotEquals { get; set; }

    [JsonPropertyName("isEmpty")]
    public bool IsEmpty { get; set; }

    [JsonPropertyName("isNotEmpty")]
    public bool IsNotEmpty { get; set; }
}

public sealed class FormAction
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "save";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "Enregistrer";

    [JsonPropertyName("isPrimary")]
    public bool IsPrimary { get; set; } = true;
}
