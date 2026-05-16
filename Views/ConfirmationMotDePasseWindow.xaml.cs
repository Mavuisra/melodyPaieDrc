using System.Windows;

namespace MelodyPaieRDC.Views;

public partial class ConfirmationMotDePasseWindow : Window
{
    public ConfirmationMotDePasseWindow()
    {
        InitializeComponent();
    }

    public string? MotDePasse => TxtMotDePasse.Password;

    private void Annuler_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Confirmer_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
