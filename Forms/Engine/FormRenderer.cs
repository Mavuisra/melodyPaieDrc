using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MelodyPaieRDC.Forms.Metadata;
using MelodyPaieRDC.Forms.ViewModels;

namespace MelodyPaieRDC.Forms.Engine;

/// <summary>
/// Construit l'interface WPF à partir des métadonnées JSON.
/// </summary>
public sealed class FormRenderer
{
    private readonly FormFieldHandlerRegistry _registry = new();

    public FormFieldHandlerRegistry Registry => _registry;

    public void Construire(Panel conteneur, DynamicFormViewModel viewModel)
    {
        conteneur.Children.Clear();

        foreach (var section in viewModel.Definition.Sections)
        {
            if (!string.IsNullOrWhiteSpace(section.Title))
            {
                conteneur.Children.Add(new TextBlock
                {
                    Text = section.Title,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 12, 0, 8)
                });
            }

            foreach (var fieldVm in viewModel.Champs.Where(c => c.SectionId == section.Id))
            {
                var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
                panel.SetBinding(UIElement.VisibilityProperty, new Binding(nameof(DynamicFieldViewModel.VisibiliteUi))
                {
                    Source = fieldVm
                });

                var label = fieldVm.Definition.Label +
                            (fieldVm.Definition.Required ? " *" : "");
                panel.Children.Add(new TextBlock
                {
                    Text = label,
                    Margin = new Thickness(0, 0, 0, 2)
                });

                if (!string.IsNullOrWhiteSpace(fieldVm.Definition.Hint))
                {
                    panel.Children.Add(new TextBlock
                    {
                        Text = fieldVm.Definition.Hint,
                        FontSize = 11,
                        Foreground = System.Windows.Media.Brushes.Gray,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 4)
                    });
                }

                var handler = _registry.Obtenir(fieldVm.Definition.Type);
                panel.Children.Add(handler.CreerControle(fieldVm, fieldVm.Definition));
                conteneur.Children.Add(panel);
            }
        }
    }
}
