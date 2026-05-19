using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class ConfigurationExportsPaieWindow : Window
{
    private readonly PaieDbContext _db = new();

    public ConfigurationExportsPaieWindow()
    {
        InitializeComponent();
        var vm = new ConfigurationExportsPaieViewModel(_db);
        vm.OnEnregistre = () => UiFeedback.Succes("Configuration des exports enregistrée.");
        DataContext = vm;
    }

    protected override void OnClosed(EventArgs e)
    {
        _db.Dispose();
        base.OnClosed(e);
    }
}
