using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class IprConfigWindow : Window
{
    private readonly PaieDbContext _db;
    private readonly IprConfigViewModel _viewModel;

    public IprConfigWindow()
    {
        InitializeComponent();

        _db = new PaieDbContext();
        _viewModel = new IprConfigViewModel(_db);
        DataContext = _viewModel;

        _viewModel.OnEnregistrementReussi += () =>
        {
            MessageBox.Show("Paramètres IPR enregistrés avec succès.", "IPR", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        };

        _viewModel.OnErreur += message =>
        {
            MessageBox.Show(message, "Erreur IPR", MessageBoxButton.OK, MessageBoxImage.Warning);
        };

        Loaded += (_, _) => _viewModel.Charger();
    }
}

