using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class TauxSociauxWindow : Window
{
    public TauxSociauxWindow()
    {
        InitializeComponent();
        var db = new PaieDbContext();
        var vm = new TauxSociauxViewModel(db);
        DataContext = vm;
        vm.OnFermer = () =>
        {
            UiFeedback.Succes("Taux sociaux enregistrés.");
            DialogResult = true;
            Close();
        };
        vm.OnErreur = msg => UiFeedback.Avertissement(msg);
        Loaded += (_, _) => vm.Charger();
    }
}
