using MelodyPaieRDC.Services;
using MelodyPaieRDC.Tests.Helpers;
using MelodyPaieRDC.ViewModels;
using QuestPDF.Infrastructure;

namespace MelodyPaieRDC.Tests;

public class ExportPdfPointageTests
{
    public ExportPdfPointageTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void ExporterMouvementsJourPdf_cree_fichier()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mouvements_test_{Guid.NewGuid():N}.pdf");
        try
        {
            var service = new ExportPdfService();
            service.ExporterMouvementsJourPdf(
                new[]
                {
                    new MouvementJourPdfLigne(
                        "11/08/2026", "001", "Jean Dupont", "Administration",
                        "08:15", "17:30", "En retard", "35 min")
                },
                new DateTime(2026, 8, 11),
                null,
                "07:40",
                path);

            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 500);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ExporterRetardsJourPdf_cree_fichier()
    {
        var path = Path.Combine(Path.GetTempPath(), $"retards_test_{Guid.NewGuid():N}.pdf");
        try
        {
            var service = new ExportPdfService();
            service.ExporterRetardsJourPdf(
                new[]
                {
                    new RetardPdfLigne(
                        "11/08/2026", "001", "Jean Dupont", "Administration",
                        "08:15", "35 min", "12.50 USD/h", "7.29 USD", "07:40")
                },
                new DateTime(2026, 8, 11),
                "07:40",
                "7.29 USD",
                "20 000 CDF",
                path);

            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 500);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ViewModel_export_mouvements_avec_pointages_du_jour()
    {
        using var factory = new PaieTestDbFactory();
        var scenario = PaieTestScenario.Creer(factory);
        var today = DateTime.Today;
        var entree = today.AddHours(8).AddMinutes(15);
        var sortie = today.AddHours(17).AddMinutes(30);
        scenario.AjouterSuiviPointages(today, new[] { entree, sortie });

        var vm = new SuiviJournalierViewModel(scenario.Db);
        vm.ChargerEmployes();
        vm.RafraichirApresChangementReglesLt();

        Assert.NotEmpty(vm.PresenceSyntheseEmployes);

        var path = Path.Combine(Path.GetTempPath(), $"vm_mouvements_{Guid.NewGuid():N}.pdf");
        try
        {
            Assert.True(vm.ExporterPointesAujourdhuiPdf(path));
            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 500);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ViewModel_export_echoue_sans_pointages()
    {
        using var factory = new PaieTestDbFactory();
        var scenario = PaieTestScenario.Creer(factory);
        var vm = new SuiviJournalierViewModel(scenario.Db);
        vm.RafraichirApresChangementReglesLt();

        var path = Path.Combine(Path.GetTempPath(), $"vm_vide_{Guid.NewGuid():N}.pdf");
        Assert.False(vm.ExporterPointesAujourdhuiPdf(path));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void ExporterBulletin_cree_pdf_format_A5()
    {
        using var factory = new PaieTestDbFactory();
        var scenario = PaieTestScenario.Creer(factory);
        var bulletin = scenario.GenererBulletin();
        var path = Path.Combine(Path.GetTempPath(), $"bulletin_a5_{Guid.NewGuid():N}.pdf");
        try
        {
            var service = new ExportPdfService();
            service.ExporterBulletin(bulletin, path);

            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 500);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ExporterBulletinsFeuilleA4_cree_pdf_2_par_page()
    {
        using var factory = new PaieTestDbFactory();
        var scenario = PaieTestScenario.Creer(factory);
        var bulletin = scenario.GenererBulletin();
        var path = Path.Combine(Path.GetTempPath(), $"bulletins_2a4_{Guid.NewGuid():N}.pdf");
        try
        {
            var service = new ExportPdfService();
            service.ExporterBulletinsFeuilleA4(new[] { bulletin }, path);

            Assert.True(File.Exists(path));
            Assert.True(new FileInfo(path).Length > 500);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
