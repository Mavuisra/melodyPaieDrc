using System.Windows;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.Views;

public partial class ChangerMotDePasseWindow : Window
{
    public ChangerMotDePasseWindow(string login)
    {
        InitializeComponent();
        TxtUtilisateur.Text = $"Utilisateur : {login}";
    }

    public string NouveauMotDePasse => TxtNouveau.Password;

    private void Annuler_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Enregistrer_Click(object sender, RoutedEventArgs e)
    {
        if (!AuthService.ValiderPolitiqueMotDePasse(TxtNouveau.Password ?? "", out var erreur))
        {
            MessageBox.Show(this, erreur, "Mot de passe", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtNouveau.Focus();
            return;
        }
        DialogResult = true;
        Close();
    }
}
