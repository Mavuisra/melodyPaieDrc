using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class SaisiePaieMoisWindow : Window
{
    public SaisiePaieMoisWindow(int periodePaieId)
    {
        InitializeComponent();
        var db = new PaieDbContext();
        var vm = new SaisiePaieMoisViewModel(db, periodePaieId);
        DataContext = vm;
        vm.OnFermer = () =>
        {
            UiFeedback.Succes("Saisie de paie enregistrée.");
            DialogResult = true;
            Close();
        };
        vm.OnErreur = msg => UiFeedback.Avertissement(msg);
        Loaded += (_, _) => vm.Charger();
    }
}

