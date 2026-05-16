using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class ConfigurationExportsPaieWindow : Window
{
    private readonly PaieDbContext _db = new();

    public ConfigurationExportsPaieWindow()
    {
        InitializeComponent();
        var vm = new ConfigurationExportsPaieViewModel(_db);
        vm.OnEnregistre = () => MessageBox.Show(this, "Configuration enregistrée.", "Exports paie",
            MessageBoxButton.OK, MessageBoxImage.Information);
        DataContext = vm;
    }

    protected override void OnClosed(EventArgs e)
    {
        _db.Dispose();
        base.OnClosed(e);
    }
}
