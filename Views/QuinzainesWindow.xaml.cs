using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;
using Microsoft.Win32;

namespace MelodyPaieRDC.Views;

public partial class QuinzainesWindow : Window
{
    public QuinzainesWindow(int? periodePaieId = null)
    {
        InitializeComponent();
        var db = new PaieDbContext();
        var vm = new QuinzainesViewModel(db, periodePaieId);
        DataContext = vm;
        vm.OnErreur = msg => UiFeedback.Avertissement(msg);
        vm.OnSucces = msg => UiFeedback.Succes(msg);
        vm.OnConfirmer = msg =>
            MessageBox.Show(this, msg, "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        vm.OnExporterPdf = (octrois, periode) =>
        {
            var dlg = new SaveFileDialog
            {
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = $"Quinzaines_{periode.Mois:D2}_{periode.Annee}.pdf",
                DefaultExt = ".pdf"
            };
            if (dlg.ShowDialog(this) != true)
                return;
            try
            {
                new ExportPdfService().ExporterOctroisQuinzainesPdf(octrois, periode.Mois, periode.Annee, dlg.FileName);
                UiFeedback.Succes("PDF des quinzaines exporté.");
            }
            catch (Exception ex)
            {
                UiFeedback.Avertissement(ex.Message);
            }
        };
        Loaded += (_, _) => vm.Charger();
    }
}
