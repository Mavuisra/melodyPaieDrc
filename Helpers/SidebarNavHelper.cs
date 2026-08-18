using System.Windows;

namespace MelodyPaieRDC.Helpers;

/// <summary>
/// Propriété attachée pour l'état actif d'un bouton de navigation sidebar (fiabilise le style visuel).
/// </summary>
public static class SidebarNavHelper
{
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.RegisterAttached(
            "IsActive",
            typeof(bool),
            typeof(SidebarNavHelper),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static void SetIsActive(DependencyObject element, bool value) => element.SetValue(IsActiveProperty, value);
    public static bool GetIsActive(DependencyObject element) => (bool)element.GetValue(IsActiveProperty);
}
