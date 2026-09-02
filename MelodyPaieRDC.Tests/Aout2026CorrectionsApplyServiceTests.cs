using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.Tests;

public class Aout2026CorrectionsApplyServiceTests
{
    [Fact]
    public void EstPeriodeCible_reconnait_aout_2026()
    {
        var ok = new PeriodePaie { Mois = 8, Annee = 2026 };
        var ko = new PeriodePaie { Mois = 9, Annee = 2026 };
        Assert.True(Aout2026CorrectionsApplyService.EstPeriodeCible(ok));
        Assert.False(Aout2026CorrectionsApplyService.EstPeriodeCible(ko));
    }

    [Fact]
    public void Catalog_contient_58_lignes_sans_lakwa()
    {
        Assert.Equal(58, Aout2026CorrectionsCatalog.Lignes.Count);
        Assert.DoesNotContain(Aout2026CorrectionsCatalog.Lignes, l => l.EmployeId == 54);
    }

    [Fact]
    public void Appliquer_refuse_periode_incorrecte()
    {
        using var factory = new Helpers.PaieTestDbFactory();
        var scenario = Helpers.PaieTestScenario.Creer(factory, anneePeriode: 2026, moisPeriode: 9);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Aout2026CorrectionsApplyService.Appliquer(scenario.Db, scenario.PeriodeId));
        Assert.Contains("Août 2026", ex.Message);
    }

    [Fact]
    public void Appliquer_rouvre_et_recloture_aout_si_periode_cloturee()
    {
        using var factory = new Helpers.PaieTestDbFactory();
        var scenario = Helpers.PaieTestScenario.Creer(factory, anneePeriode: 2026, moisPeriode: 8);
        var periode = scenario.Db.PeriodesPaie.First(p => p.Id == scenario.PeriodeId);
        periode.Cloturee = true;
        periode.DateClotureUtc = DateTime.UtcNow;
        periode.CloturePar = "test";
        scenario.Db.SaveChanges();

        var septembre = new PeriodePaie
        {
            Mois = 9,
            Annee = 2026,
            TauxChangeBudget = 2800m,
            Cloturee = false,
            EntrepriseId = scenario.EntrepriseId
        };
        scenario.Db.PeriodesPaie.Add(septembre);
        scenario.Db.SaveChanges();

        var result = Aout2026CorrectionsApplyService.Appliquer(scenario.Db, scenario.PeriodeId);

        periode = scenario.Db.PeriodesPaie.First(p => p.Id == scenario.PeriodeId);
        Assert.True(periode.Cloturee);
        Assert.NotNull(periode.DateClotureUtc);
        Assert.Equal("test", periode.CloturePar);

        var septembreApres = scenario.Db.PeriodesPaie.First(p => p.Id == septembre.Id);
        Assert.False(septembreApres.Cloturee);

        Assert.Contains(result.Avertissements, a => a.Contains("re-clôturée automatiquement"));
    }
}
