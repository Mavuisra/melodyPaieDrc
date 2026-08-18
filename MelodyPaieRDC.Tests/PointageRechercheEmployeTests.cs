using MelodyPaieRDC.Tests.Helpers;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Tests;

public class PointageRechercheEmployeTests : IDisposable
{
    private readonly PaieTestDbFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public void Recherche_suggere_et_selectionne_lemploye()
    {
        var scenario = PaieTestScenario.Creer(_factory);
        scenario.Db.SetTenant(scenario.EntrepriseId);

        var vm = new SuiviJournalierViewModel(scenario.Db);
        vm.ChargerEmployes();
        vm.RechercheEmployeText = "TST";

        Assert.True(vm.AfficherSuggestionsEmployes);
        Assert.Contains(vm.SuggestionsEmployes, e => e.Id == scenario.EmployeId);

        vm.SelectionnerPremiereSuggestionEmploye();

        Assert.NotNull(vm.EmployeSelectionne);
        Assert.Equal(scenario.EmployeId, vm.EmployeSelectionne!.Id);
        Assert.False(vm.AfficherSuggestionsEmployes);
        Assert.Contains(vm.EmployeSelectionne.Matricule, vm.RechercheEmployeText);
    }
}
