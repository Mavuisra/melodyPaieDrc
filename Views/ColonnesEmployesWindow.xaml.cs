using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class ColonnesEmployesWindow : Window
{
    public ColonnesEmployesWindow()
    {
        InitializeComponent();
        var vm = new ColonnesEmployesViewModel(new PaieDbContext());
        vm.OnEnregistre = () =>
        {
            UiFeedback.Succes("Colonnes enregistrées.");
            DialogResult = true;
        };
        DataContext = vm;
    }
}
