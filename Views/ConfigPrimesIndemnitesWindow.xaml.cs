using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class ConfigPrimesIndemnitesWindow : Window
{
    public ConfigPrimesIndemnitesWindow()
    {
        InitializeComponent();
        var db = new PaieDbContext();
        var vm = new ConfigPrimesIndemnitesViewModel(db);
        DataContext = vm;
        vm.OnFermer = () =>
        {
            UiFeedback.Succes("Primes et indemnités enregistrées.");
            DialogResult = true;
            Close();
        };
        vm.OnErreur = msg => UiFeedback.Avertissement(msg);
        Loaded += (_, _) => vm.Charger();
    }
}
