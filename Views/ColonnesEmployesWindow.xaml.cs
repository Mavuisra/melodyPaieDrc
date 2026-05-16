using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class ColonnesEmployesWindow : Window
{
    public ColonnesEmployesWindow()
    {
        InitializeComponent();
        var vm = new ColonnesEmployesViewModel(new PaieDbContext());
        vm.OnEnregistre = () =>
        {
            MessageBox.Show(this, "Colonnes enregistrées. Rouvrez le menu Employés pour appliquer.", "Configuration",
                MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        };
        DataContext = vm;
    }
}
