using System.Windows;
using Microsoft.Win32;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Views;

public partial class EntrepriseWindow : Window
{
    public EntrepriseWindow()
    {
        InitializeComponent();
        var db = new PaieDbContext();
        var vm = new EntrepriseViewModel(db);
        DataContext = vm;
        vm.OnErreur = msg => MessageBox.Show(msg, "Informations entreprise", MessageBoxButton.OK, MessageBoxImage.Warning);
        vm.OnEnregistre = () => MessageBox.Show("Informations enregistrées.", "Informations entreprise", MessageBoxButton.OK, MessageBoxImage.Information);
        vm.OnDemandeChoisirLogo = () =>
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Tous les fichiers (*.*)|*.*",
                Title = "Choisir un logo"
            };
            if (dlg.ShowDialog(this) == true)
                vm.DefinirLogoDepuisFichier(dlg.FileName);
        };
        Loaded += (_, _) => vm.Charger();
    }

    private void Fermer_Click(object sender, RoutedEventArgs e) => Close();
}
