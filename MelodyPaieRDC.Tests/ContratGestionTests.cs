using MelodyPaieRDC.Services;
using MelodyPaieRDC.Tests.Helpers;
using MelodyPaieRDC.ViewModels;
using QuestPDF.Infrastructure;

namespace MelodyPaieRDC.Tests;

public class ContratGestionTests
{
    public ContratGestionTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void ExporterContratPdf_cree_fichier_pdf()
    {
        using var factory = new PaieTestDbFactory();
        var scenario = PaieTestScenario.Creer(factory, salaireBase: 300m);
        scenario.Db.SetTenant(scenario.EntrepriseId);

        var path = Path.Combine(Path.GetTempPath(), $"contrat_test_{Guid.NewGuid():N}.pdf");
        try
        {
            new ExportPdfService().ExporterContratPdf(scenario.ContratId, path, scenario.Db);

            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 500);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void ContratEditViewModel_charge_et_reflete_modifications()
    {
        using var factory = new PaieTestDbFactory();
        var scenario = PaieTestScenario.Creer(factory, salaireBase: 3000m);
        scenario.Db.SetTenant(scenario.EntrepriseId);

        var vm = new ContratEditViewModel(scenario.Db, scenario.ContratId);
        vm.Charger();

        Assert.Equal("Test Employé", vm.NomEmploye);
        Assert.Equal(3000m, vm.Contrat.SalaireBase);
        Assert.Equal("CDI", vm.Contrat.TypeContrat);

        vm.Contrat.SalaireBase = 4500m;
        vm.Contrat.TauxMajorationHeuresSup = 75m;
        vm.Contrat.PreavisMoisBase = 2m;

        var entite = scenario.Db.Contrats.Find(scenario.ContratId)!;
        entite.SalaireBase = vm.Contrat.SalaireBase;
        entite.TauxMajorationHeuresSup = vm.Contrat.TauxMajorationHeuresSup;
        entite.PreavisMoisBase = vm.Contrat.PreavisMoisBase;
        scenario.Db.SaveChanges();

        using var db2 = factory.CreateContext();
        db2.SetTenant(scenario.EntrepriseId);
        var reloaded = db2.Contrats.Find(scenario.ContratId)!;

        Assert.Equal(4500m, reloaded.SalaireBase);
        Assert.Equal(75m, reloaded.TauxMajorationHeuresSup);
        Assert.Equal(2m, reloaded.PreavisMoisBase);
    }

    [Fact]
    public void Contrat_suppression_reussie_sans_bulletin()
    {
        using var factory = new PaieTestDbFactory();
        var scenario = PaieTestScenario.Creer(factory);
        scenario.Db.SetTenant(scenario.EntrepriseId);

        var entite = scenario.Db.Contrats.Find(scenario.ContratId)!;
        scenario.Db.Contrats.Remove(entite);
        scenario.Db.SaveChanges();

        Assert.False(scenario.Db.Contrats.Any(c => c.Id == scenario.ContratId));
    }

    [Fact]
    public void FinContratViewModel_calcule_preavis_et_indemnite()
    {
        using var factory = new PaieTestDbFactory();
        var scenario = PaieTestScenario.Creer(factory, salaireBase: 1000m);
        scenario.Db.SetTenant(scenario.EntrepriseId);

        var contrat = scenario.Db.Contrats.Find(scenario.ContratId)!;
        contrat.PreavisMoisBase = 1.5m;
        contrat.IndemniteLicenciementMoisBase = 2m;
        scenario.Db.SaveChanges();

        var vm = new FinContratViewModel(scenario.Db, scenario.EmployeId);
        vm.Charger();

        Assert.Equal("Test Employé", vm.NomEmploye);
        Assert.Equal(1000m, vm.SalaireDeBase);
        Assert.Equal(1500m, vm.PreavisMontant);
        Assert.Equal(2000m, vm.IndemniteLicenciementMontant);
        Assert.NotNull(vm.ContratActif);
    }

    [Fact]
    public void ContratViewModel_selectionne_automatiquement_le_contrat()
    {
        using var factory = new PaieTestDbFactory();
        var scenario = PaieTestScenario.Creer(factory);
        scenario.Db.SetTenant(scenario.EntrepriseId);

        var vm = new ContratViewModel(scenario.Db, scenario.EmployeId);
        vm.Charger();

        Assert.Equal(1, vm.NbContrats);
        Assert.False(vm.AfficherFormulaireAjout);
        Assert.NotNull(vm.Selectionne);
        Assert.Equal(scenario.ContratId, vm.Selectionne!.Id);
        Assert.True(vm.ExporterPdfCommand.CanExecute(null));
    }

    [Fact]
    public void ContratViewModel_recharge_donnees_apres_modification_externe()
    {
        using var factory = new PaieTestDbFactory();
        var scenario = PaieTestScenario.Creer(factory, salaireBase: 2000m);
        scenario.Db.SetTenant(scenario.EntrepriseId);

        var vm = new ContratViewModel(scenario.Db, scenario.EmployeId);
        vm.Charger();
        Assert.Equal(2000m, vm.Selectionne!.SalaireBase);

        // Simule l'enregistrement depuis ContratEditWindow (autre contexte)
        using (var dbEdit = factory.CreateContext())
        {
            dbEdit.SetTenant(scenario.EntrepriseId);
            var entite = dbEdit.Contrats.Find(scenario.ContratId)!;
            entite.SalaireBase = 3500m;
            dbEdit.SaveChanges();
        }

        vm.NotifierContratModifie();
        Assert.Equal(3500m, vm.Selectionne!.SalaireBase);
        Assert.Equal(3500m / 26m, vm.Selectionne.SalaireJour, 2);
    }
}
