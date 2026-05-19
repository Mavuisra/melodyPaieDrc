using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
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
        vm.OnErreur = msg => UiFeedback.Avertissement(msg);
        Loaded += (_, _) =>
        {
            vm.Charger();
            Title = "Prêts et avances – " + (string.IsNullOrEmpty(vm.NomEmploye) ? "Employé" : vm.NomEmploye);
        };
    }
}
