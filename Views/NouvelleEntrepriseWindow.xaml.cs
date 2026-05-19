using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.Views;

public partial class NouvelleEntrepriseWindow : Window
{
    public int? EntrepriseCreeeId { get; private set; }

    public NouvelleEntrepriseWindow()
    {
        InitializeComponent();
        if (!DroitsUi.PeutModifier)
        {
            TxtRaisonSociale.IsReadOnly = true;
            BtnCreer.IsEnabled = false;
        }
        Loaded += (_, _) => TxtRaisonSociale.Focus();
    }

    private void Annuler_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Creer_Click(object sender, RoutedEventArgs e)
    {
        if (!DroitsUi.PeutModifier)
            return;

        var nom = TxtRaisonSociale.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(nom))
        {
            MessageBox.Show(this, "Indiquez la raison sociale.", "Nouvelle entreprise",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtRaisonSociale.Focus();
            return;
        }

        try
        {
            using var db = new PaieDbContext();
            EntrepriseCreeeId = EntrepriseOnboardingService.CreerNouvelleEntreprise(db, nom);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Nouvelle entreprise", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
