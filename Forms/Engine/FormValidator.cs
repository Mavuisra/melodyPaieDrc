using System.Globalization;
using System.Text.RegularExpressions;
using MelodyPaieRDC.Forms.Metadata;
using MelodyPaieRDC.Forms.ViewModels;

namespace MelodyPaieRDC.Forms.Engine;

/// <summary>
/// Valide les champs selon les règles déclarées dans les métadonnées JSON.
/// </summary>
public static class FormValidator
{
    public static bool Valider(
        FormDefinition definition,
        IReadOnlyList<DynamicFieldViewModel> champs,
        FormFieldHandlerRegistry registry,
        out string messageErreur)
    {
        foreach (var section in definition.Sections)
        {
            foreach (var fieldDef in section.Fields)
            {
                var vm = champs.FirstOrDefault(c => c.Key == fieldDef.Key);
                if (vm == null || !vm.EstVisible) continue;

                var handler = registry.Obtenir(fieldDef.Type);
                var valeur = handler.NormaliserValeur(vm.Value, fieldDef);

                if (fieldDef.Required && string.IsNullOrWhiteSpace(valeur))
                {
                    messageErreur = $"Le champ « {fieldDef.Label} » est obligatoire.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(valeur)) continue;

                if (fieldDef.MaxLength.HasValue && valeur.Length > fieldDef.MaxLength.Value)
                {
                    messageErreur = $"« {fieldDef.Label} » : maximum {fieldDef.MaxLength} caractères.";
                    return false;
                }

                if (fieldDef.Type.Equals("number", StringComparison.OrdinalIgnoreCase))
                {
                    if (!decimal.TryParse(valeur, NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
                    {
                        messageErreur = $"« {fieldDef.Label} » doit être un nombre.";
                        return false;
                    }
                    if (fieldDef.Min.HasValue && n < fieldDef.Min.Value)
                    {
                        messageErreur = $"« {fieldDef.Label} » : minimum {fieldDef.Min}.";
                        return false;
                    }
                    if (fieldDef.Max.HasValue && n > fieldDef.Max.Value)
                    {
                        messageErreur = $"« {fieldDef.Label} » : maximum {fieldDef.Max}.";
                        return false;
                    }
                }

                if (fieldDef.Type.Equals("email", StringComparison.OrdinalIgnoreCase) &&
                    !Regex.IsMatch(valeur, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase))
                {
                    messageErreur = $"« {fieldDef.Label} » : adresse e-mail invalide.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(fieldDef.Pattern) &&
                    !Regex.IsMatch(valeur, fieldDef.Pattern))
                {
                    messageErreur = $"« {fieldDef.Label} » : format invalide.";
                    return false;
                }
            }
        }

        messageErreur = "";
        return true;
    }
}
