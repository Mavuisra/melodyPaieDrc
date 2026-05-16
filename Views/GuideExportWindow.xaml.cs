using System.Diagnostics;
using System.Windows;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.Views;

public partial class GuideExportWindow : Window
{
    public GuideExportWindow(string titre, string contenuMarkdown, bool afficherLienCnss = false)
    {
        InitializeComponent();
        Title = titre;
        TxtGuide.Text = contenuMarkdown;
        BtnOuvrirPortail.Visibility = afficherLienCnss ? Visibility.Visible : Visibility.Collapsed;
    }

    public static void AfficherCnss(Window owner)
    {
        var w = new GuideExportWindow("Guide CNSS e-déclaration", GuidesExportService.LireGuideCnss(), true)
        {
            Owner = owner
        };
        w.ShowDialog();
    }

    public static void AfficherIpr(Window owner)
    {
        var w = new GuideExportWindow("Guide déclaration IPR DGI", GuidesExportService.LireGuideIpr())
        {
            Owner = owner
        };
        w.ShowDialog();
    }

    private void OuvrirPortail_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://edeclaration.cnss.cd") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Navigateur", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Fermer_Click(object sender, RoutedEventArgs e) => Close();
}
