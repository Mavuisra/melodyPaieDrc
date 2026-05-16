using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MelodyPaieRDC.Helpers;

/// <summary>
/// Archivé le 2026-05-16 : déclaré dans MainWindow.xaml mais jamais utilisé dans un Binding.
/// Conservé ici pour référence ; la version active a été retirée de EqualityToVisibilityConverter.cs.
/// </summary>
public class InverseEqualityToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var result = new EqualityToVisibilityConverter().Convert(value, targetType, parameter, culture);
        return result is Visibility v && v == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
