using System.Globalization;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Data;

/// <summary>
/// Données de référence paie RDC (barème, taux, rubriques) — configurables, sans client codé en dur.
/// </summary>
public static class DonneesPaieReferenceSeed
{
    public static void SeedReferentielLegalSiVide(PaieDbContext db, int? entrepriseId = null)
    {
        if (!db.GrillesIpr.Any(g => g.EntrepriseId == entrepriseId))
        {
            foreach (var tranche in BaremeIprRdc2020())
                db.GrillesIpr.Add(new GrilleIPR { EntrepriseId = entrepriseId, BorneInf = tranche.inf, BorneSup = tranche.sup, Taux = tranche.taux });
            db.SaveChanges();
        }

        if (!db.ParametresIpr.Any(p => p.EntrepriseId == entrepriseId))
        {
            db.ParametresIpr.Add(new ParametreIPR { EntrepriseId = entrepriseId, TauxEffectifMaximum = 0m, ReductionParEnfant = 0m });
            db.SaveChanges();
        }

        if (!db.TauxSociaux.Any(t => t.EntrepriseId == entrepriseId))
        {
            db.TauxSociaux.AddRange(new[]
            {
                new TauxSociaux { EntrepriseId = entrepriseId, Code = "CNSS_Ouvrier", Pourcentage = 5m },
                new TauxSociaux { EntrepriseId = entrepriseId, Code = "CNSS_Patronal", Pourcentage = 13m },
                new TauxSociaux { EntrepriseId = entrepriseId, Code = "INPP", Pourcentage = 3m },
                new TauxSociaux { EntrepriseId = entrepriseId, Code = "ONEM", Pourcentage = 0.5m }
            });
            db.SaveChanges();
        }
    }

    public static void SeedCategoriesSiVide(PaieDbContext db, int? entrepriseId = null)
    {
        if (db.CategoriesProfessionnelles.Any(c => c.EntrepriseId == entrepriseId))
            return;

        db.CategoriesProfessionnelles.Add(new CategorieProfessionnelle { EntrepriseId = entrepriseId, Libelle = "Cadre", SmigApplique = 0m });
        db.CategoriesProfessionnelles.Add(new CategorieProfessionnelle { EntrepriseId = entrepriseId, Libelle = "Maîtrise", SmigApplique = 0m });
        db.CategoriesProfessionnelles.Add(new CategorieProfessionnelle { EntrepriseId = entrepriseId, Libelle = "Exécution", SmigApplique = 0m });
        db.SaveChanges();
    }

    public static void SeedPrimesCourantesSiVide(PaieDbContext db, int? entrepriseId = null)
    {
        if (db.PrimesIndemnites.Any(p => p.EntrepriseId == entrepriseId))
            return;

        var ordre = 10;
        void Ajouter(string libelle, bool imposable, bool cotisable, string type = PrimeIndemnite.TypeAvantage)
        {
            db.PrimesIndemnites.Add(new PrimeIndemnite
            {
                EntrepriseId = entrepriseId,
                Libelle = libelle,
                EstImposable = imposable,
                EstCotisable = cotisable,
                ModeCalcul = PrimeIndemnite.ModeFixe,
                TypeLigne = type,
                OrdreAffichage = ordre
            });
            ordre += 10;
        }

        Ajouter("Prime d'ancienneté", true, true);
        Ajouter("Prime de rendement", true, true);
        Ajouter("Indemnité de transport", false, false);
        Ajouter("Indemnité de logement", true, true);
        db.SaveChanges();
    }

    public static PolitiquePaie CreerPolitiqueParDefaut(int entrepriseId)
    {
        var politique = new PolitiquePaie
        {
            EntrepriseId = entrepriseId,
            Libelle = "Politique par défaut",
            DateEffet = DateTime.Today,
            Actif = true,
            Version = "1.0",
            UpdatedAtUtc = DateTime.UtcNow,
            Parametres = new List<ParametrePolitiquePaie>
            {
                new() { Cle = ParametrePolitiquePaie.Cles.JoursReferencePaie, Valeur = "26" },
                new() { Cle = ParametrePolitiquePaie.Cles.HeuresParJour, Valeur = "8" },
                new() { Cle = ParametrePolitiquePaie.Cles.SalaireContratEnNet, Valeur = "false" },
                new() { Cle = ParametrePolitiquePaie.Cles.ModeCalculPresence, Valeur = ParametrePolitiquePaie.ModePresencePointages },
                new() { Cle = ParametrePolitiquePaie.Cles.UtiliserBaremeIpr, Valeur = "true" },
                new() { Cle = ParametrePolitiquePaie.Cles.UtiliserTauxSociauxDb, Valeur = "true" }
            },
            Rubriques = CreerRubriquesParDefaut()
        };
        return politique;
    }

    public static List<RubriqueBulletin> CreerRubriquesParDefaut()
    {
        return new List<RubriqueBulletin>
        {
            Rub("SALAIRE_BASE_JOUR", "Salaire de base proratisé", 10, RubriqueBulletin.TypeGain),
            Rub("HEURES_LT_PERIODE", "Heures période", 20, RubriqueBulletin.TypeInfo),
            Rub("ABSENCE_INFO", "Absence", 30, RubriqueBulletin.TypeInfo),
            Rub("ABSENCE_NON_REMUNEREE", "Absence non rémunérée", 40, RubriqueBulletin.TypeRetenue),
            Rub("SUSPENSION_CONTRAT", "Suspension contrat", 50, RubriqueBulletin.TypeRetenue),
            Rub("AUTRES_GAINS_IMPOSABLES", "Autres gains imposables", 60, RubriqueBulletin.TypeGain),
            Rub("AUTRES_GAINS_NON_IMPOSABLES", "Autres gains non imposables", 70, RubriqueBulletin.TypeGain),
            Rub("IPR", "IPR", 80, RubriqueBulletin.TypeRetenue, RubriqueBulletin.SourceIprBareme),
            Rub("CNSS", "CNSS ouvrier", 90, RubriqueBulletin.TypeRetenue, RubriqueBulletin.SourceCnssOuvrier),
            Rub("INPP", "INPP", 100, RubriqueBulletin.TypeRetenue, RubriqueBulletin.SourceInpp),
            Rub("PRETS_AVANCES", "Prêts / avances", 110, RubriqueBulletin.TypeRetenue),
            Rub("ACOMPTES_SALAIRE", "Acomptes salaire", 120, RubriqueBulletin.TypeRetenue),
            Rub("SANCTIONS_DISCIPLINAIRES", "Sanctions disciplinaires", 130, RubriqueBulletin.TypeRetenue),
            Rub("AJUSTEMENTS_RETENUES", "Ajustements retenues", 140, RubriqueBulletin.TypeRetenue)
        };
    }

    private static RubriqueBulletin Rub(string code, string libelle, int ordre, string type, string source = RubriqueBulletin.SourceAucune)
        => new()
        {
            Code = code,
            Libelle = libelle,
            OrdreAffichage = ordre,
            TypeLigne = type,
            SourceCalcul = source,
            AfficherSurBulletin = true
        };

    private static IEnumerable<(decimal inf, decimal sup, decimal taux)> BaremeIprRdc2020()
    {
        yield return (0m, 162000m, 3m);
        yield return (162001m, 1800000m, 15m);
        yield return (1800001m, 3600000m, 30m);
        yield return (3600001m, 0m, 40m);
    }
}
