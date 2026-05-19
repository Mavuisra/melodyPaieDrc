using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class ContratsWindow : Window
{
    private readonly int _employeId;

    public ContratsWindow(int employeId)
    {
        InitializeComponent();
        _employeId = employeId;
        var db = new PaieDbContext();
        var vm = new ContratViewModel(db, employeId);
        DataContext = vm;
        vm.OnErreur = msg => UiFeedback.Avertissement(msg);
        Loaded += (_, _) =>
        {
            vm.Charger();
            Title = "Contrats – " + (string.IsNullOrEmpty(vm.NomEmploye) ? "Employé" : vm.NomEmploye);
        };
    }

    private void FinContrat_Click(object sender, RoutedEventArgs e)
    {
        var win = new FinContratWindow(_employeId) { Owner = this };
        win.ShowDialog();
    }
}
