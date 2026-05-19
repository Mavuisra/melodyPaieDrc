using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class DefinitionChampsWindow : Window
{
    public DefinitionChampsWindow()
    {
        InitializeComponent();
        var vm = new DefinitionChampsViewModel(new PaieDbContext());
        vm.OnErreur = msg => UiFeedback.Avertissement(msg);
        DataContext = vm;
    }
}
