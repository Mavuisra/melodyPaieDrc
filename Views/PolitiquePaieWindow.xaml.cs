using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class PolitiquePaieWindow : Window
{
    public PolitiquePaieWindow()
    {
        InitializeComponent();
        var vm = new PolitiquePaieViewModel(new PaieDbContext());
        vm.OnSucces = msg => { MessageBox.Show(this, msg, "Politique de paie", MessageBoxButton.OK, MessageBoxImage.Information); DialogResult = true; };
        vm.OnErreur = msg => MessageBox.Show(this, msg, "Politique de paie", MessageBoxButton.OK, MessageBoxImage.Warning);
        DataContext = vm;
    }
}
