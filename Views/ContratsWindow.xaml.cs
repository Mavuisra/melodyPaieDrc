using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class ContratsWindow : Window
{
    private readonly int _employeId;
    private ContratViewModel _vm = null!;

    public ContratsWindow(int employeId)
    {
        InitializeComponent();
        _employeId = employeId;
        var db = new PaieDbContext();
        _vm = new ContratViewModel(db, employeId);
        DataContext = _vm;
        _vm.OnErreur = msg => UiFeedback.Avertissement(msg);
        _vm.OnDemandeModification = OuvrirModification;
        _vm.OnDemandeExportPdf = ExporterContratPdf;
        Loaded += (_, _) =>
        {
            _vm.Charger();
            Title = "Contrats – " + (string.IsNullOrEmpty(_vm.NomEmploye) ? "Employé" : _vm.NomEmploye);
        };
    }

    private void OuvrirModification(int contratId)
    {
        var win = new ContratEditWindow(contratId) { Owner = this };
        if (win.ShowDialog() == true)
        {
            _vm.NotifierContratModifie();
            Title = "Contrats – " + (string.IsNullOrEmpty(_vm.NomEmploye) ? "Employé" : _vm.NomEmploye);
        }
    }

    private void ExporterContratPdf(int contratId)
    {
        var contrat = _vm.Contrats.FirstOrDefault(c => c.Id == contratId);
        var nomEmploye = _vm.NomEmploye.Replace(" ", "_");
        var type = contrat?.TypeContrat ?? "Contrat";
        var dlg = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = $"Contrat_{type}_{nomEmploye}.pdf",
            Title = "Exporter le contrat en PDF"
        };
        if (dlg.ShowDialog(this) != true)
            return;

        try
        {
            new ExportPdfService().ExporterContratPdf(contratId, dlg.FileName);
            UiFeedback.Succes($"Contrat exporté : {Path.GetFileName(dlg.FileName)}");
        }
        catch (Exception ex)
        {
            UiFeedback.Avertissement($"Export impossible : {ex.Message}");
        }
    }

    private void FinContrat_Click(object sender, RoutedEventArgs e)
    {
        var win = new FinContratWindow(_employeId) { Owner = this };
        win.ShowDialog();
    }
}
