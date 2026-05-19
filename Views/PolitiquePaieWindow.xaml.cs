using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class PolitiquePaieWindow : Window
{
    public PolitiquePaieWindow()
    {
        InitializeComponent();
        var vm = new PolitiquePaieViewModel(new PaieDbContext());
        vm.OnSucces = msg => { UiFeedback.Succes(msg); DialogResult = true; };
        vm.OnErreur = msg => UiFeedback.Avertissement(msg);
        DataContext = vm;
    }
}
