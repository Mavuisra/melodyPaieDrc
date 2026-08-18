using MelodyPaieRDC.Models;
using MelodyPaieRDC.Tests.Helpers;
using MelodyPaieRDC.ViewModels;

namespace MelodyPaieRDC.Tests;

public class PretsAvancesViewModelTests : IDisposable
{
    private readonly PaieTestDbFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public void CalculerSoldeRestant_conserve_les_montants_deja_preleves()
    {
        Assert.Equal(250m, PretsAvancesViewModel.CalculerSoldeRestant(400m, 150m));
        Assert.Equal(0m, PretsAvancesViewModel.CalculerSoldeRestant(100m, 150m));
    }

    [Fact]
    public void Modifier_met_a_jour_montant_echeances_et_solde()
    {
        var scenario = PaieTestScenario.Creer(_factory);
        scenario.Db.SetTenant(scenario.EntrepriseId);

        var pret = new PretAvance
        {
            EmployeId = scenario.EmployeId,
            MontantTotal = 400m,
            DateOctroi = new DateTime(2024, 1, 5),
            DateDebutEcheance = new DateTime(2024, 1, 5),
            NbEcheances = 4,
            MontantMensuel = 100m,
            SoldeRestant = 300m,
            Statut = "En cours"
        };
        scenario.Db.PretsAvances.Add(pret);
        scenario.Db.SaveChanges();

        var vm = new PretsAvancesViewModel(scenario.Db, scenario.EmployeId)
        {
            ConfirmerAction = (_, _) => true
        };
        vm.Charger();
        vm.Selectionne = vm.PretsAvances.Single();
        vm.ChargerPourModification();

        Assert.True(vm.EstEnEdition);
        vm.MontantTotal = 500m;
        vm.NbEcheances = 5;
        vm.EnregistrerCommand.Execute(null);

        var reloaded = scenario.Db.PretsAvances.Single();
        Assert.Equal(500m, reloaded.MontantTotal);
        Assert.Equal(5, reloaded.NbEcheances);
        Assert.Equal(100m, reloaded.MontantMensuel);
        Assert.Equal(400m, reloaded.SoldeRestant);
        Assert.Equal("En cours", reloaded.Statut);
        Assert.False(vm.EstEnEdition);
    }

    [Fact]
    public void Supprimer_fonctionne_meme_si_lemploye_a_deja_ete_paye()
    {
        var scenario = PaieTestScenario.Creer(_factory);
        scenario.Db.SetTenant(scenario.EntrepriseId);
        scenario.DefinirModePresenceSaisieJours(26);

        scenario.Db.PretsAvances.Add(new PretAvance
        {
            EmployeId = scenario.EmployeId,
            MontantTotal = 200m,
            DateOctroi = new DateTime(2024, 1, 1),
            DateDebutEcheance = new DateTime(2024, 1, 1),
            NbEcheances = 2,
            MontantMensuel = 100m,
            SoldeRestant = 200m,
            Statut = "En cours"
        });
        scenario.Db.SaveChanges();
        scenario.GenererBulletin();

        var vm = new PretsAvancesViewModel(scenario.Db, scenario.EmployeId)
        {
            ConfirmerAction = (_, _) => true
        };
        vm.Charger();
        Assert.True(vm.EmployeDejaPaye);
        vm.Selectionne = vm.PretsAvances.Single();
        vm.SupprimerCommand.Execute(null);

        Assert.Empty(scenario.Db.PretsAvances.ToList());
        Assert.Empty(vm.PretsAvances);
    }
}
