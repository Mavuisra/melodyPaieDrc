using System.Windows;
using System.Windows.Controls;
using MelodyPaieRDC.Forms.Metadata;
using MelodyPaieRDC.Forms.ViewModels;

namespace MelodyPaieRDC.Forms.Engine;

/// <summary>
/// Registre extensible des gestionnaires de types de champs.
/// </summary>
public sealed class FormFieldHandlerRegistry
{
    private readonly Dictionary<string, IFormFieldHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);

    public FormFieldHandlerRegistry()
    {
        Enregistrer(new TextFieldHandler());
        Enregistrer(new TextAreaFieldHandler());
        Enregistrer(new NumberFieldHandler());
        Enregistrer(new DateFieldHandler());
        Enregistrer(new BooleanFieldHandler());
        Enregistrer(new ChoiceFieldHandler());
        Enregistrer(new LookupFieldHandler());
    }

    public void Enregistrer(IFormFieldHandler handler) => _handlers[handler.TypeName] = handler;

    public IFormFieldHandler Obtenir(string type)
    {
        var key = string.IsNullOrWhiteSpace(type) ? "text" : type.Trim();
        if (key.Equals("email", StringComparison.OrdinalIgnoreCase))
            key = "text";
        return _handlers.TryGetValue(key, out var h) ? h : _handlers["text"];
    }

    public IEnumerable<string> TypesEnregistres => _handlers.Keys.OrderBy(k => k);
}

internal sealed class TextFieldHandler : IFormFieldHandler
{
    public string TypeName => "text";

    public FrameworkElement CreerControle(DynamicFieldViewModel field, FieldDefinition definition)
    {
        var tb = new TextBox
        {
            MaxLength = definition.MaxLength ?? 500,
            IsReadOnly = definition.ReadOnly,
            Margin = new Thickness(0, 0, 0, 8)
        };
        tb.SetBinding(TextBox.TextProperty, field.Binding(nameof(DynamicFieldViewModel.Value)));
        return tb;
    }

    public string? NormaliserValeur(string? rawValue, FieldDefinition definition) =>
        string.IsNullOrWhiteSpace(rawValue) ? null : rawValue.Trim();
}

internal sealed class TextAreaFieldHandler : IFormFieldHandler
{
    public string TypeName => "textarea";

    public FrameworkElement CreerControle(DynamicFieldViewModel field, FieldDefinition definition)
    {
        var tb = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 72,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxLength = definition.MaxLength ?? 2000,
            IsReadOnly = definition.ReadOnly,
            Margin = new Thickness(0, 0, 0, 8)
        };
        tb.SetBinding(TextBox.TextProperty, field.Binding(nameof(DynamicFieldViewModel.Value)));
        return tb;
    }

    public string? NormaliserValeur(string? rawValue, FieldDefinition definition) =>
        string.IsNullOrWhiteSpace(rawValue) ? null : rawValue.Trim();
}

internal sealed class NumberFieldHandler : IFormFieldHandler
{
    public string TypeName => "number";

    public FrameworkElement CreerControle(DynamicFieldViewModel field, FieldDefinition definition)
    {
        var tb = new TextBox
        {
            IsReadOnly = definition.ReadOnly,
            Margin = new Thickness(0, 0, 0, 8)
        };
        tb.SetBinding(TextBox.TextProperty, field.Binding(nameof(DynamicFieldViewModel.Value)));
        return tb;
    }

    public string? NormaliserValeur(string? rawValue, FieldDefinition definition) =>
        string.IsNullOrWhiteSpace(rawValue) ? null : rawValue.Trim().Replace(',', '.');
}

internal sealed class DateFieldHandler : IFormFieldHandler
{
    public string TypeName => "date";

    public FrameworkElement CreerControle(DynamicFieldViewModel field, FieldDefinition definition)
    {
        var dp = new DatePicker
        {
            IsEnabled = !definition.ReadOnly,
            Margin = new Thickness(0, 0, 0, 8)
        };
        dp.SelectedDateChanged += (_, _) =>
        {
            field.Value = dp.SelectedDate?.ToString("yyyy-MM-dd");
        };
        if (DateTime.TryParse(field.Value, out var d))
            dp.SelectedDate = d;
        field.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(DynamicFieldViewModel.Value)) return;
            if (DateTime.TryParse(field.Value, out var parsed))
                dp.SelectedDate = parsed;
            else
                dp.SelectedDate = null;
        };
        return dp;
    }

    public string? NormaliserValeur(string? rawValue, FieldDefinition definition) =>
        string.IsNullOrWhiteSpace(rawValue) ? null : rawValue.Trim();
}

internal sealed class BooleanFieldHandler : IFormFieldHandler
{
    public string TypeName => "boolean";

    public FrameworkElement CreerControle(DynamicFieldViewModel field, FieldDefinition definition)
    {
        var cb = new CheckBox
        {
            IsEnabled = !definition.ReadOnly,
            Margin = new Thickness(0, 0, 0, 8),
            VerticalAlignment = VerticalAlignment.Center
        };
        cb.Checked += (_, _) => field.Value = "true";
        cb.Unchecked += (_, _) => field.Value = "false";
        cb.IsChecked = string.Equals(field.Value, "true", StringComparison.OrdinalIgnoreCase);
        return cb;
    }

    public string? NormaliserValeur(string? rawValue, FieldDefinition definition) =>
        string.Equals(rawValue, "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false";
}

internal sealed class ChoiceFieldHandler : IFormFieldHandler
{
    public string TypeName => "choice";

    public FrameworkElement CreerControle(DynamicFieldViewModel field, FieldDefinition definition)
    {
        var cb = new ComboBox
        {
            IsEnabled = !definition.ReadOnly,
            IsEditable = true,
            Margin = new Thickness(0, 0, 0, 8)
        };
        if (definition.Choices != null)
            foreach (var c in definition.Choices)
                cb.Items.Add(c);
        cb.SetBinding(ComboBox.TextProperty, field.Binding(nameof(DynamicFieldViewModel.Value)));
        return cb;
    }

    public string? NormaliserValeur(string? rawValue, FieldDefinition definition) =>
        string.IsNullOrWhiteSpace(rawValue) ? null : rawValue.Trim();
}

internal sealed class LookupFieldHandler : IFormFieldHandler
{
    public string TypeName => "lookup";

    public FrameworkElement CreerControle(DynamicFieldViewModel field, FieldDefinition definition)
    {
        var cb = new ComboBox
        {
            IsEnabled = !definition.ReadOnly,
            DisplayMemberPath = "Label",
            SelectedValuePath = "Value",
            Margin = new Thickness(0, 0, 0, 8)
        };
        foreach (var opt in field.LookupOptions)
            cb.Items.Add(opt);
        cb.SetBinding(ComboBox.SelectedValueProperty, field.Binding(nameof(DynamicFieldViewModel.Value)));
        return cb;
    }

    public string? NormaliserValeur(string? rawValue, FieldDefinition definition) =>
        string.IsNullOrWhiteSpace(rawValue) ? null : rawValue.Trim();
}
