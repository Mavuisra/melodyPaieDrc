using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class PrimesIndemnitesEmployeWindow : Window
{
    public PrimesIndemnitesEmployeWindow(int employeId)
    {
        InitializeComponent();
        var db = new PaieDbContext();
        var vm = new PrimesIndemnitesEmployeViewModel(db, employeId);
        DataContext = vm;
        vm.OnErreur = msg => MessageBox.Show(msg, "Primes et indemnités", MessageBoxButton.OK, MessageBoxImage.Warning);
        Loaded += (_, _) =>
        {
            vm.Charger();
            Title = $"Primes et indemnités – {vm.NomEmploye}";
        };
    }
}
