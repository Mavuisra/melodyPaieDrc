using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
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
        vm.OnErreur = msg => UiFeedback.Avertissement(msg);
        Loaded += (_, _) =>
        {
            vm.Charger();
            Title = $"Primes et indemnités – {vm.NomEmploye}";
        };
    }
}
