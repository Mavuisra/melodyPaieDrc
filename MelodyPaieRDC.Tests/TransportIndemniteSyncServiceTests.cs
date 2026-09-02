using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Services;
using MelodyPaieRDC.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Tests;

public class TransportIndemniteSyncServiceTests
{
    [Fact]
    public void SynchroniserPourTous_met_62_40_pour_tous_les_employes()
    {
        using var factory = new PaieTestDbFactory();
        var scenario = PaieTestScenario.Creer(factory);
        using var db = scenario.Db;
        db.SetTenant(scenario.EntrepriseId);

        scenario.AjouterPrime(TransportIndemniteDefaults.LibellePrime, 94.90m, estImposable: false, estCotisable: false);

        TransportIndemniteSyncService.SynchroniserPourTous(db);

        var montant = db.AffectationsPrimesIndemnites
            .Include(a => a.PrimeIndemnite)
            .Where(a => a.EmployeId == scenario.EmployeId)
            .AsEnumerable()
            .First(a => TransportAbsencePaieHelper.EstIndemniteTransport(a.PrimeIndemnite!.Libelle))
            .Montant;

        Assert.Equal(TransportIndemniteDefaults.MontantMensuelUsd, montant);
    }

    [Fact]
    public void SynchroniserPourTous_retire_transport_des_stagiaires()
    {
        using var factory = new PaieTestDbFactory();
        var scenario = PaieTestScenario.Creer(factory);
        using var db = scenario.Db;
        db.SetTenant(scenario.EntrepriseId);

        var contrat = db.Contrats.Find(scenario.ContratId)!;
        contrat.TypeContrat = "Stage";
        scenario.AjouterPrime(TransportIndemniteDefaults.LibellePrime, 62.40m, estImposable: false, estCotisable: false);
        db.SaveChanges();

        TransportIndemniteSyncService.SynchroniserPourTous(db);

        var aEncoreTransport = db.AffectationsPrimesIndemnites
            .Include(a => a.PrimeIndemnite)
            .Where(a => a.EmployeId == scenario.EmployeId)
            .AsEnumerable()
            .Any(a => TransportAbsencePaieHelper.EstIndemniteTransport(a.PrimeIndemnite!.Libelle));
        Assert.False(aEncoreTransport);
    }
}
