using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class FinContratWindow : Window
{
    public FinContratWindow(int employeId)
    {
        InitializeComponent();
        var db = new PaieDbContext();
        var vm = new FinContratViewModel(db, employeId);
        DataContext = vm;
        Loaded += (_, _) => vm.Charger();
    }

    private void Fermer_Click(object sender, RoutedEventArgs e) => Close();
}
