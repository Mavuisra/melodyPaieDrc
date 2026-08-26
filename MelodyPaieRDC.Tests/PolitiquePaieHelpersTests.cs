using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.Tests;

public class PeriodePaieHelperTests
{
    [Fact]
    public void Periode_calendaire_couvre_le_mois_entier()
    {
        var periode = new PeriodePaie { Mois = 3, Annee = 2026 };
        var politique = PolitiqueCalendaire();

        var (debut, fin) = PeriodePaieHelper.ObtenirBornes(periode, politique);

        Assert.Equal(new DateTime(2026, 3, 1), debut);
        Assert.Equal(new DateTime(2026, 3, 31), fin);
    }

    [Fact]
    public void Periode_decalee_26_25_pour_mars_2026()
    {
        var periode = new PeriodePaie { Mois = 3, Annee = 2026 };
        var politique = PolitiqueDecalee();

        var (debut, fin) = PeriodePaieHelper.ObtenirBornes(periode, politique);

        Assert.Equal(new DateTime(2026, 2, 26), debut);
        Assert.Equal(new DateTime(2026, 3, 25), fin);
    }

    private static PolitiquePaieContext PolitiqueCalendaire() =>
        new(new PolitiquePaie(), new Dictionary<string, string>(), Array.Empty<RubriqueBulletin>());

    private static PolitiquePaieContext PolitiqueDecalee()
    {
        var parametres = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ParametrePolitiquePaie.Cles.TypePeriodePaie] = ParametrePolitiquePaie.TypePeriodeDecalee,
            [ParametrePolitiquePaie.Cles.JourDebutPeriodeDecalee] = "26",
            [ParametrePolitiquePaie.Cles.JourFinPeriodeDecalee] = "25"
        };
        return new PolitiquePaieContext(new PolitiquePaie(), parametres, Array.Empty<RubriqueBulletin>());
    }
}

public class RetardPaieHelperTests
{
    [Fact]
    public void Sanction_horaire_des_la_premiere_minute()
    {
        var parametres = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ParametrePolitiquePaie.Cles.RetardSanctionActive] = "true",
            [ParametrePolitiquePaie.Cles.RetardSeuilMinutes] = "1",
            [ParametrePolitiquePaie.Cles.RetardModeSanction] = ParametrePolitiquePaie.RetardModeHoraire
        };
        var politique = new PolitiquePaieContext(new PolitiquePaie(), parametres, Array.Empty<RubriqueBulletin>());
        // 1 min × 3 $/h = 0,05 $
        var montant = RetardPaieHelper.CalculerSanctionJour(politique, 1, 24m, 3m);
        Assert.Equal(0.05m, montant);
    }

    [Fact]
    public void Sanction_demi_jour_apres_seuil_2h()
    {
        var politique = PolitiqueRetardDemiJour();
        var montant = RetardPaieHelper.CalculerSanctionJour(politique, 121, 100m, 12.5m);
        Assert.Equal(50m, montant);
    }

    [Fact]
    public void Sanction_zero_sous_le_seuil()
    {
        var politique = PolitiqueRetardDemiJour();
        var montant = RetardPaieHelper.CalculerSanctionJour(politique, 119, 100m, 12.5m);
        Assert.Equal(0m, montant);
    }

    [Fact]
    public void Tolérance_individuelle_remplace_entreprise()
    {
        var entreprise = LtServicesRegles.Defaut;
        var employe = new Employe { HeureLimiteTolerance = "09:00" };
        var regles = RetardPaieHelper.ReglesPourEmploye(entreprise, employe);
        Assert.Equal(new TimeSpan(9, 0, 0), regles.HeureLimiteTolerance);
    }

    private static PolitiquePaieContext PolitiqueRetardDemiJour()
    {
        var parametres = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ParametrePolitiquePaie.Cles.RetardSanctionActive] = "true",
            [ParametrePolitiquePaie.Cles.RetardSeuilMinutes] = "120",
            [ParametrePolitiquePaie.Cles.RetardModeSanction] = ParametrePolitiquePaie.RetardModeDemiJour
        };
        return new PolitiquePaieContext(new PolitiquePaie(), parametres, Array.Empty<RubriqueBulletin>());
    }
}
