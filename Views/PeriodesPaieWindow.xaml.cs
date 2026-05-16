using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class PeriodesPaieWindow : Window
{
    public PeriodesPaieWindow()
    {
        InitializeComponent();
        var db = new PaieDbContext();
        var vm = new PeriodesPaieViewModel(db);
        DataContext = vm;
        vm.OnSucces = msg => MessageBox.Show(msg, "Enregistré", MessageBoxButton.OK, MessageBoxImage.Information);
        vm.OnErreur = msg => MessageBox.Show(msg, "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        vm.OuvrirAssistantCloture = id =>
        {
            if (new CloturePeriodeWindow(id) { Owner = this }.ShowDialog() == true)
                vm.Charger();
        };
        Loaded += (_, _) => vm.Charger();
    }
}
