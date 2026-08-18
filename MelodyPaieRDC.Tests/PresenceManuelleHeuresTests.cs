using MelodyPaieRDC.Models;
using MelodyPaieRDC.Tests.Helpers;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Tests;

public class PresenceManuelleHeuresTests : IDisposable
{
    private readonly PaieTestDbFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public void MarquerPresence_signe_la_journee_avec_les_heures_normales()
    {
        var scenario = PaieTestScenario.Creer(_factory, anneePeriode: 2024, moisPeriode: 1);
        scenario.Db.SetTenant(scenario.EntrepriseId);

        var vm = new HeuresPresteesTotauxViewModel(scenario.Db);
        vm.PeriodeSelectionnee = vm.PeriodesPaie.First(p => p.Id == scenario.PeriodeId);
        vm.EmployeSelectionne = vm.Employes.First(e => e.Id == scenario.EmployeId);

        var jour = new DateTime(2024, 1, 8);
        var cell = vm.CellulesCalendrier.Single(c => c.Date == jour && c.EstDansMoisVisible);
        vm.SelectionnerJourCommand.Execute(cell);
        Assert.NotNull(vm.DetailJour);

        vm.MarquerPresenceCommand.Execute(null);

        var suivi = scenario.Db.SuivisJournaliers.Single(s =>
            s.EmployeId == scenario.EmployeId && s.Date.Date == jour);
        Assert.Equal(SuiviJournalier.TypeNormal, suivi.TypeJour);
        Assert.True(suivi.HeuresManuelles);
        Assert.True(suivi.HeuresPrestees > 0m);
        Assert.Equal(suivi.HeuresPrestees, vm.DetailJour!.HeuresPrestees);
    }

    [Fact]
    public void MarquerPresence_refuse_un_jour_non_ouvrable()
    {
        var scenario = PaieTestScenario.Creer(_factory, anneePeriode: 2024, moisPeriode: 1);
        scenario.Db.SetTenant(scenario.EntrepriseId);

        var vm = new HeuresPresteesTotauxViewModel(scenario.Db);
        vm.PeriodeSelectionnee = vm.PeriodesPaie.First(p => p.Id == scenario.PeriodeId);
        vm.EmployeSelectionne = vm.Employes.First(e => e.Id == scenario.EmployeId);

        var dimanche = new DateTime(2024, 1, 7);
        var cell = vm.CellulesCalendrier.Single(c => c.Date == dimanche && c.EstDansMoisVisible);
        vm.SelectionnerJourCommand.Execute(cell);
        vm.MarquerPresenceCommand.Execute(null);

        Assert.False(scenario.Db.SuivisJournaliers.Any(s =>
            s.EmployeId == scenario.EmployeId && s.Date.Date == dimanche));
    }
}
