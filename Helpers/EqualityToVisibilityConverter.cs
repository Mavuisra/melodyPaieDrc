using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MelodyPaieRDC.Helpers;

/// <summary>
/// MultiBinding: [0]=MenuSelectionne (int), [1]=Tag (index). Retourne true si égaux.
/// Pour afficher l'état sélectionné d'un bouton de menu (Tag="0", "1", ...).
/// </summary>
public class MenuIndexEqualsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2) return false;
        var a = values[0] is int i ? i : (values[0] != null && int.TryParse(values[0].ToString(), out var i2) ? i2 : -1);
        var b = values[1] != null && int.TryParse(values[1].ToString(), out var j) ? j : -2;
        return a == b;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Retourne Visible si la valeur bindée (int) est égale au paramètre (int), sinon Collapsed.
/// ConverterParameter="1" pour afficher quand la valeur vaut 1.
/// </summary>
public class EqualityToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var param = parameter?.ToString();
        if (string.IsNullOrEmpty(param) || value is not int intVal)
            return Visibility.Collapsed;
        if (!int.TryParse(param, out int target))
            return Visibility.Collapsed;
        return intVal == target ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Pour lier TextBox.Text (string) à une propriété int. Chaîne vide ou invalide → 0.
/// </summary>
public class StringToIntConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int i ? i.ToString(culture) : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && int.TryParse(s.Trim(), NumberStyles.Integer, culture, out var n) ? n : 0;
}

/// <summary>
/// Retourne Collapsed si la valeur est "CDI" (masquer date fin pour CDI), sinon Visible.
/// </summary>
public class CdiToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var s = value?.ToString()?.Trim() ?? "";
        return string.Equals(s, "CDI", StringComparison.OrdinalIgnoreCase) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Visible si la chaîne n'est pas vide, Collapsed sinon.
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => !string.IsNullOrEmpty(value?.ToString()) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Pour lier TextBox.Text (string) à une propriété decimal. Chaîne vide ou invalide → 0.
/// </summary>
public class StringToDecimalConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is decimal d ? d.ToString(culture) : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && decimal.TryParse(s, NumberStyles.Any, culture, out var n) ? n : 0m;
}
