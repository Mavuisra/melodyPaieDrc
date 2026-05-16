using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class PretsAvancesWindow : Window
{
    public PretsAvancesWindow(int employeId)
    {
        InitializeComponent();
        var db = new PaieDbContext();
        var vm = new PretsAvancesViewModel(db, employeId);
        DataContext = vm;
        vm.OnErreur = msg => MessageBox.Show(msg, "Prêts et avances", MessageBoxButton.OK, MessageBoxImage.Warning);
        Loaded += (_, _) =>
        {
            vm.Charger();
            Title = "Prêts et avances – " + (string.IsNullOrEmpty(vm.NomEmploye) ? "Employé" : vm.NomEmploye);
        };
    }
}
