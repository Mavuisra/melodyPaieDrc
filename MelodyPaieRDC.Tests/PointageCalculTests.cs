using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services;

namespace MelodyPaieRDC.Tests;

public class SuiviJournalierCalculPaieHelperTests
{
    [Fact]
    public void Journee_complete_lt_equivaut_a_un_jour_pondere()
    {
        var lundi = new DateTime(2024, 1, 8);
        var heuresNominales = LtServicesRegles.Defaut.HeuresNormalesJourSemaine;
        var suivi = new SuiviJournalier
        {
            Date = lundi,
            TypeJour = SuiviJournalier.TypeNormal,
            HeuresPrestees = heuresNominales,
            HeuresManuelles = true
        };

        var jours = SuiviJournalierCalculPaieHelper.CalculerJoursEquivalentsPaie(
            new[] { suivi },
            semaineSixJours: false,
            calendrier: new Dictionary<DateTime, JourTravailCalendrier>());

        Assert.Equal(1m, jours);
    }

    [Fact]
    public void Samedi_5h_equivaut_a_un_jour_en_semaine_6_jours()
    {
        var samedi = new DateTime(2024, 1, 6);
        var suivi = new SuiviJournalier
        {
            Date = samedi,
            TypeJour = SuiviJournalier.TypeNormal,
            HeuresPrestees = 5m,
            HeuresManuelles = true
        };

        var jours = SuiviJournalierCalculPaieHelper.CalculerJoursEquivalentsPaie(
            new[] { suivi },
            semaineSixJours: true,
            calendrier: new Dictionary<DateTime, JourTravailCalendrier>());

        Assert.Equal(1m, jours);
    }

    [Fact]
    public void Heures_manuelles_sont_prises_en_compte_sans_pointages()
    {
        var suivi = new SuiviJournalier
        {
            Date = new DateTime(2024, 1, 8),
            TypeJour = SuiviJournalier.TypeNormal,
            HeuresPrestees = 10m,
            HeuresManuelles = true
        };

        var heures = SuiviJournalierCalculPaieHelper.RecalculerHeuresEffectives(suivi);
        var heuresSup = SuiviJournalierCalculPaieHelper.CalculerHeuresSupplementairesJour(suivi, 8m);

        Assert.Equal(10m, heures);
        Assert.Equal(2m, heuresSup);
    }

    [Fact]
    public void DeterminerHeuresNominalesJour_samedi_5h_en_semaine_6_jours()
    {
        var samedi = new DateTime(2024, 1, 6);
        var nominal = SuiviJournalierCalculPaieHelper.DeterminerHeuresNominalesJour(
            samedi,
            semaineSixJours: true,
            calendrier: new Dictionary<DateTime, JourTravailCalendrier>());

        Assert.Equal(5m, nominal);
    }
}

public class LtServicesPointageCalculTests
{
    [Fact]
    public void CalculerHeuresApresFinOfficielle_compte_heures_apres_16h()
    {
        var lundi = new DateTime(2024, 1, 8);
        var pointages = new List<DateTime>
        {
            lundi.Add(LtServicesPointageCalcul.HeureDebutTravail),
            lundi.Date.AddHours(12),
            lundi.Date.AddHours(13),
            lundi.Date.AddHours(18)
        };

        var apresFin = LtServicesPointageCalcul.CalculerHeuresApresFinOfficielle(pointages, lundi);

        Assert.Equal(2m, apresFin);
    }

    [Fact]
    public void CalculerHeuresSupplementaires_inclut_depassement_nominal()
    {
        var lundi = new DateTime(2024, 1, 8);
        var pointages = new List<DateTime>
        {
            lundi.Add(LtServicesPointageCalcul.HeureDebutTravail),
            lundi.Date.AddHours(12),
            lundi.Date.AddHours(13),
            lundi.Date.AddHours(18)
        };

        var heuresSup = LtServicesPointageCalcul.CalculerHeuresSupplementaires(
            pointages, lundi, heuresNominalesJour: 8m);

        Assert.True(heuresSup >= 2m);
    }
}
