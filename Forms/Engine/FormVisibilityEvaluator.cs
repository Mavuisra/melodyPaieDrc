using MelodyPaieRDC.Forms.Metadata;
using MelodyPaieRDC.Forms.ViewModels;

namespace MelodyPaieRDC.Forms.Engine;

/// <summary>
/// Évalue les conditions visibleWhen des métadonnées.
/// </summary>
public static class FormVisibilityEvaluator
{
    public static bool EstVisible(FieldDefinition field, IReadOnlyDictionary<string, string?> valeurs)
    {
        var cond = field.VisibleWhen;
        if (cond == null || string.IsNullOrWhiteSpace(cond.Field))
            return true;

        valeurs.TryGetValue(cond.Field, out var refValue);
        refValue ??= "";

        if (cond.IsEmpty)
            return string.IsNullOrWhiteSpace(refValue);
        if (cond.IsNotEmpty)
            return !string.IsNullOrWhiteSpace(refValue);
        if (cond.EqualsValue != null)
            return string.Equals(refValue, cond.EqualsValue, StringComparison.OrdinalIgnoreCase);
        if (cond.NotEquals != null)
            return !string.Equals(refValue, cond.NotEquals, StringComparison.OrdinalIgnoreCase);

        return true;
    }

    public static void MettreAJourVisibilite(IEnumerable<DynamicFieldViewModel> champs)
    {
        var snapshot = champs.ToDictionary(c => c.Key, c => c.Value);
        foreach (var champ in champs)
            champ.EstVisible = EstVisible(champ.Definition, snapshot);
    }
}
