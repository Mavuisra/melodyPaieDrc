using System.Windows;
using MelodyPaieRDC.Forms.Metadata;
using MelodyPaieRDC.Forms.ViewModels;

namespace MelodyPaieRDC.Forms.Engine;

/// <summary>
/// Contrat d'extension pour un type de champ personnalisé (enregistré dans FormFieldHandlerRegistry).
/// Permet d'ajouter de nouveaux types sans modifier le moteur principal.
/// </summary>
public interface IFormFieldHandler
{
    string TypeName { get; }

    FrameworkElement CreerControle(DynamicFieldViewModel field, FieldDefinition definition);

    string? NormaliserValeur(string? rawValue, FieldDefinition definition);
}
