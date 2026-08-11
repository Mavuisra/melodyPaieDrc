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

        static int AsInt(object? v, CultureInfo cult, int fallback)
        {
            if (v is int i) return i;
            if (v is string s && int.TryParse(s, NumberStyles.Integer, cult, out var parsed)) return parsed;
            if (v != null && int.TryParse(v.ToString(), NumberStyles.Integer, cult, out parsed)) return parsed;
            return fallback;
        }

        var selected = AsInt(values[0], culture, -1);
        var tag = AsInt(values[1], culture, -2);
        return selected == tag;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>MenuSelectionne (int) vs ConverterParameter (index Tag) → bool pour SidebarNavHelper.IsActive.</summary>
public class MenuIndexActiveConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not int selected)
            return false;
        if (parameter is int tag)
            return selected == tag;
        return int.TryParse(parameter?.ToString(), NumberStyles.Integer, culture, out var parsed) && selected == parsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
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
