using System.Windows;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class MiseAJourWindow : Window
{
    private readonly MiseAJourViewModel _viewModel;

    public MiseAJourWindow()
    {
        InitializeComponent();
        _viewModel = new MiseAJourViewModel();
        _viewModel.DemanderFermeture = () =>
        {
            DialogResult = false;
            Close();
        };
        _viewModel.DemanderArretApplication = () =>
        {
            DialogResult = true;
            Application.Current.Shutdown();
        };
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.VerifierAuChargementAsync();
    }

    public static void Afficher(Window? proprietaire)
    {
        var win = new MiseAJourWindow
        {
            Owner = proprietaire
        };
        win.ShowDialog();
    }
}
