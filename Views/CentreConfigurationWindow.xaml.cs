using System.Windows;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.Views;

public partial class CentreConfigurationWindow : Window
{
    public Action? OuvrirPolitiquePaie { get; set; }
    public Action? OuvrirIpr { get; set; }
    public Action? OuvrirTauxSociaux { get; set; }
    public Action? OuvrirPrimes { get; set; }
    public Action? OuvrirEntreprise { get; set; }
    public Action? OuvrirEtablissements { get; set; }
    public Action? OuvrirColonnes { get; set; }
    public Action? OuvrirChamps { get; set; }
    public Action? OuvrirFormulaires { get; set; }
    public Action? OuvrirPeriodes { get; set; }
    public Action? OuvrirExportsPaie { get; set; }
    public Action? OuvrirAssistant { get; set; }

    public CentreConfigurationWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => RafraichirEnteteEntreprise();
        AppSessionEvents.EntrepriseCouranteChanged += OnProfilEntrepriseModifie;
        Closed += (_, _) => AppSessionEvents.EntrepriseCouranteChanged -= OnProfilEntrepriseModifie;
    }

    private void OnProfilEntrepriseModifie() =>
        Dispatcher.Invoke(RafraichirEnteteEntreprise);

    private void RafraichirEnteteEntreprise()
    {
        using var db = new PaieDbContext();
        var id = ContexteEntrepriseService.ObtenirEntrepriseCouranteId(db);
        var libelle = ContexteEntrepriseService.ObtenirRaisonSocialeCourante(db) ?? $"Entreprise #{id}";
        DataContext = new { EntrepriseLibelle = libelle };
    }

    private void PolitiquePaie_Click(object sender, RoutedEventArgs e) { OuvrirPolitiquePaie?.Invoke(); }
    private void Ipr_Click(object sender, RoutedEventArgs e) { OuvrirIpr?.Invoke(); }
    private void TauxSociaux_Click(object sender, RoutedEventArgs e) { OuvrirTauxSociaux?.Invoke(); }
    private void Primes_Click(object sender, RoutedEventArgs e) { OuvrirPrimes?.Invoke(); }
    private void Entreprise_Click(object sender, RoutedEventArgs e) { OuvrirEntreprise?.Invoke(); }
    private void Etablissements_Click(object sender, RoutedEventArgs e) { OuvrirEtablissements?.Invoke(); }
    private void Colonnes_Click(object sender, RoutedEventArgs e) { OuvrirColonnes?.Invoke(); }
    private void Champs_Click(object sender, RoutedEventArgs e) { OuvrirChamps?.Invoke(); }
    private void Formulaires_Click(object sender, RoutedEventArgs e) { OuvrirFormulaires?.Invoke(); }
    private void Periodes_Click(object sender, RoutedEventArgs e) { OuvrirPeriodes?.Invoke(); }
    private void ExportsPaie_Click(object sender, RoutedEventArgs e) { OuvrirExportsPaie?.Invoke(); }
    private void Assistant_Click(object sender, RoutedEventArgs e) { OuvrirAssistant?.Invoke(); }
}
