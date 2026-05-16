using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class DefinitionChampsWindow : Window
{
    public DefinitionChampsWindow()
    {
        InitializeComponent();
        var vm = new DefinitionChampsViewModel(new PaieDbContext());
        vm.OnErreur = msg => MessageBox.Show(this, msg, "Champs personnalisés", MessageBoxButton.OK, MessageBoxImage.Warning);
        DataContext = vm;
    }
}
