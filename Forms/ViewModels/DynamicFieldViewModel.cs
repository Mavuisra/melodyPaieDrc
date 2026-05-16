using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using MelodyPaieRDC.Forms.Metadata;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.Forms.ViewModels;

public sealed class DynamicFieldViewModel : INotifyPropertyChanged
{
    private string? _value;
    private bool _estVisible = true;

    public DynamicFieldViewModel(string sectionId, FieldDefinition definition, IReadOnlyList<LookupOption> lookupOptions)
    {
        SectionId = sectionId;
        Definition = definition;
        Key = definition.Key;
        LookupOptions = lookupOptions;
        _value = definition.DefaultValue;
    }

    public string SectionId { get; }
    public FieldDefinition Definition { get; }
    public string Key { get; }
    public IReadOnlyList<LookupOption> LookupOptions { get; }

    public string? Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            OnPropertyChanged();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool EstVisible
    {
        get => _estVisible;
        set
        {
            if (_estVisible == value) return;
            _estVisible = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VisibiliteUi));
        }
    }

    public System.Windows.Visibility VisibiliteUi =>
        EstVisible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? ValueChanged;

    public Binding Binding(string propertyName) => new(propertyName)
    {
        Source = this,
        Mode = BindingMode.TwoWay,
        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
    };

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
