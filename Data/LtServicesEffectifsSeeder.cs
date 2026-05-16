using System;
using System.Linq;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Data;

/// <summary>
/// Données initiales STE LTSERVICES SARL (fichier de configuration client).
/// </summary>
public static class LtServicesEffectifsSeeder
{
    public static readonly string[] NomsDepartements =
    {
        "Administration", "Ajustage", "Chauffeur", "Electricité - Climatisation", "Magasin",
        "Mécanique", "Nettoyage", "Peinture", "Residence", "Tolerie"
    };

    private sealed record LtEmployeLigne(
        string Matricule,
        string Nom,
        string? Postnom,
        string? Prenom,
        DateTime? DateNaissance,
        string? Sexe,
        string Departement,
        string TypeContrat,
        decimal SalaireBase,
        string? NumCnss,
        string? Adresse,
        DateTime DateDebutContrat);

    private static readonly LtEmployeLigne[] Effectifs =
    {
        new("23002", "MOKILI", "MOKOLI", "Christian", new DateTime(1998, 11, 11), "M", "Administration", "CDD", 150m, null, "13ene RUE industriel n°5432 C/limete", new DateTime(2023, 8, 1)),
        new("15012", "LUBOYA", "MUKUDI", "Trésor", new DateTime(1985, 7, 13), "M", "Administration", "CDI", 900m, "106418507130G", "av,Tshela n°142 C/ de Kinshasa ", new DateTime(2015, 10, 1)),
        new("21001", "MOTANDA", "MOKOTU", "Auguy", new DateTime(1977, 11, 21), "M", "Administration", "CDI", 550m, "102737711210D", "av, MITUALITE n° 21 C/ masina", new DateTime(2021, 11, 15)),
        new("24002", "MANZAMBI", "KUBUNZILA", "Merveille", new DateTime(2005, 10, 8), "F", "Administration", "CDD", 120m, null, "av, lomami n° 7590 C/limete  ", new DateTime(2024, 5, 7)),
        new("15016", "WAMBA", "MATANDO", "Sylvie", new DateTime(1971, 12, 15), "F", "Administration", "CDI", 700m, "208417112150R", "av, kongolo n°129 C, C/de Nkinshasa", new DateTime(2015, 11, 21)),
        new("21002", "NKISHI", "MAKANGO", "Eric", new DateTime(1983, 4, 24), "M", "Administration", "CDI", 967.47m, "010313222W1", "av,telecom n°5A  C/Ngaliema", new DateTime(2021, 9, 6)),
        new("08002", "MBULUKASU", "KALALA", "Richard", new DateTime(1969, 10, 10), "M", "Ajustage", "CDI", 375m, "107556910101Q", "av,betambe n°27, C/Mt, Ngafula", new DateTime(2008, 4, 8)),
        new("12001", "IPALO", "MVOVONYENE", "BRUNO", new DateTime(1968, 2, 15), "M", "Chauffeur", "CDI", 455m, null, "av, mbali n° 183, C/masina", new DateTime(2012, 5, 2)),
        new("23001", "NSEMI", "PUAUI", "Joel", new DateTime(1983, 9, 12), "M", "Electricité - Climatisation", "CDI", 310m, null, "av, kimbala n° 19 C/ makala", new DateTime(2025, 7, 5)),
        new("19001", "MAVUNGU", "MAVANGU", "Willy", new DateTime(1984, 3, 16), "M", "Electricité - Climatisation", "CDI", 252m, "11984529231H", "AV, Anunga n°32 C/matete ", new DateTime(2019, 2, 19)),
        new("15008", "NDUALU", "LANAU", "Faguy", new DateTime(1992, 7, 3), "M", "Electricité - Climatisation", "CDI", 345m, "108339207031Q", "av, makolo n° 04 C/ Ngaliema ", new DateTime(2015, 6, 1)),
        new("21002-R18", "MVOMONYENE", "IPAPO", "Fabrice", new DateTime(1993, 11, 9), "M", "Magasin", "CDI", 350m, null, "av, ngina n° 32 C/lemba ", new DateTime(2021, 11, 5)),
        new("13005", "MAKABI", "MATAVANGA", "Chrétien", new DateTime(1980, 12, 12), "M", "Magasin", "CDI", 375m, "108338012120B", "av, du marche n° 18 C/Mt, Ngafula ", new DateTime(2013, 6, 1)),
        new("15018", "XAVIER", "BRURCLAIR", "Benjamin", new DateTime(1981, 10, 10), "M", "Mécanique", "CDI", 900m, "11981530323A", "av, bokiki n° 02, C/Lemba ", new DateTime(2015, 11, 25)),
        new("15001", "PINDI", "KABAKA", "Jean", new DateTime(1972, 6, 5), "M", "Mécanique", "CDI", 254m, "109347206061L", "av, kitona n° 30 C/selembao ", new DateTime(2015, 6, 1)),
        new("15014", "WAWA", "BABA", "Doudou", new DateTime(1985, 8, 24), "M", "Mécanique", "CDI", 204m, "109538508241N", "av, rai n°08 BUS C/masina", new DateTime(2015, 5, 2)),
        new("19006", "NDONGALA", "LUYUYE", "Rodin", new DateTime(1991, 5, 5), "M", "Mécanique", "CDI", 239m, "11991529254D", "av, regideso n° 16 C/ ngaliema ", new DateTime(2019, 4, 4)),
        new("18002", "BOSOMI", "KASA", "Toussaint", new DateTime(1992, 5, 25), "M", "Mécanique", "CDI", 317m, "11992529264T", "av, bandundu n°29 C/Mt, Ngafula", new DateTime(2018, 5, 4)),
        new("19003", "NKATE", "MUTUMBO", "Reagan", new DateTime(1988, 3, 11), "M", "Mécanique", "CDI", 275m, null, "av, kimbala n°19, C/ barumbu ", new DateTime(2019, 3, 29)),
        new("23002-R27", "LUFU", "MPUPU", "Chela", new DateTime(1996, 4, 12), "M", "Mécanique", "CDI", 202m, null, "av, parlement n°14 C/ngaliema ", new DateTime(2023, 1, 9)),
        new("24001", "MAHIKA", "METE", "Apolinaire", new DateTime(1976, 4, 5), "M", "Mécanique", "CDI", 315m, null, "av, kimiala n° 41 C/celembao ", new DateTime(2024, 3, 8)),
        new("24002-R29", "NTILA", "MASASA", "Jean", new DateTime(1988, 3, 10), "M", "Mécanique", "CDI", 375m, null, "av, bungudi n° 08 C/makala ", new DateTime(2024, 3, 8)),
        new("24003", "MONSHEME", "MONOHEME", "Glody", new DateTime(1990, 9, 9), "M", "Mécanique", "CDI", 161m, null, "av, bikuku n°27 C/masina ", new DateTime(2024, 3, 8)),
        new("11001", "MISIRI", "MPIPI", "Faustin", new DateTime(1966, 7, 6), "M", "Nettoyage", "CDI", 263m, null, "av, ndambu n° 60 C/ ngaba ", new DateTime(2011, 6, 4)),
        new("19017", "LOKAFO", "DAKA", "Fiston", new DateTime(1983, 9, 15), "M", "Nettoyage", "CDI", 175m, null, "av, de la paix n° 25, C/ matete", new DateTime(2019, 9, 23)),
        new("24002-R38", "MAMBWENI", "MAMAWENI", "Adolphe", new DateTime(1968, 11, 11), "M", "Nettoyage", "CDI", 125m, null, "av, binza n° 16 C/Mt, ngafula ", new DateTime(2024, 3, 1)),
        new("15017", "BOKOLE", "DJOJBO", "Jean Bedel", new DateTime(1974, 4, 17), "M", "Nettoyage", "CDI", 251m, "11974530297B", "av, budjala n°104, C/kitambo ", new DateTime(2016, 2, 18)),
        new("16006", "TUSEVO", "MABAILA", "Thomas", new DateTime(1965, 4, 5), "M", "Peinture", "CDI", 375m, null, "av, lomami n° 16  C/ de kinshasa", new DateTime(2016, 10, 18)),
        new("13003", "KISEMA", "NZIZGA", "Flory", new DateTime(1981, 12, 14), "M", "Peinture", "CDI", 231m, "108528112140H", "av,bandundu n°94 C/ kitambo", new DateTime(2013, 1, 2)),
        new("19002", "MAMBWANA", "KWEWUVUKILA", "Daniel", new DateTime(1974, 4, 18), "M", "Peinture", "CDI", 304m, null, "av, yanda n° 22 C/celembao", new DateTime(2019, 2, 19)),
        new("13004", "MIMI", "DITIWILA", "NICO", new DateTime(1980, 4, 24), "M", "Peinture", "CDI", 318m, "11980410146P", "av, bayombo n°45 C/makala", new DateTime(2013, 1, 2)),
        new("15015", "MASIYA", "NGEGA", "Patchely", new DateTime(1983, 3, 25), "M", "Peinture", "CDI", 196m, "102718303250S", "av, ipolo n° 12 C/ngaliema ", new DateTime(2015, 5, 2)),
        new("24002-R45", "PIERRE", "LEMA", "Heritier", new DateTime(1984, 1, 1), "M", "Peinture", "CDI", 198m, null, "av, mikasi n° 10 C/makala ", new DateTime(2024, 2, 6)),
        new("19010", "JONAS", "PEMEELE", "Andy", new DateTime(1986, 5, 5), "M", "Peinture", "CDI", 201m, null, "av, omanga n° 23, C/Mt, ngafula", new DateTime(2019, 2, 19)),
        new("19009", "MAKAMBILA", "KUBUENA", "Ibrahim", new DateTime(1972, 12, 26), "M", "Residence", "CDI", 250m, null, "av, zobia n° 83 C/ kimbaseke", new DateTime(2019, 1, 10)),
        new("21003", "NDONDA", "SUKUENGO", "Jean", null, "M", "Residence", "CDI", 275m, null, null, new DateTime(2021, 10, 20)),
        new("13002", "KANDA", "NSISNA", "André", new DateTime(1962, 6, 23), "M", "Tolerie", "CDI", 285m, "108842620623N", "av, ntuala n° 15 C/ kimbaseke", new DateTime(2013, 1, 2)),
        new("24001-R55", "LANDU", "MUAUGA", "Mathonet", new DateTime(1980, 7, 30), "M", "Tolerie", "CDI", 165m, null, "av, mayulu n°05 C/makala ", new DateTime(2024, 2, 6)),
        new("19015", "NDANGALA", "NSASI", "Noel", new DateTime(1960, 12, 25), "M", "Tolerie", "CDI", 184m, null, "av, mbanza nsundi n°99/c, C/Kimbanseke", new DateTime(2020, 3, 17)),
        new("20002", "LANDU", "NGWGLA", "Steve", new DateTime(1993, 3, 25), "M", "Tolerie", "CDD", 259m, null, "av, baladi n° 8 C/makala ", new DateTime(2025, 12, 8)),
        // Employés présents dans la configuration client sans matricule : on ajoute un matricule technique TMP-xxxx.
        new("TMP-0012", "KUBUENA", "MAKAMBILA", "Alliance", new DateTime(2005, 9, 28), "M", "Electricité - Climatisation", "Stage", 75m, null, "av, zobia n° 83 C/ kimbaseke", new DateTime(2026, 1, 12)),
        new("TMP-0013", "MIFITA", "KUPA", "Elvis", new DateTime(1992, 3, 18), "M", "Electricité - Climatisation", "Stage", 75m, null, "av, yosombote n° 71 C/masina ", new DateTime(2025, 12, 8)),
        new("TMP-0015", "MIFITA", "KUPA", "Gilbert", new DateTime(1949, 5, 27), "M", "Magasin", "CDD", 175m, null, "av, yosombote n° 71 C/masina ", new DateTime(2022, 11, 5)),
        new("TMP-0027", "MAMAUYA", "MVUAKA", "Tichick", new DateTime(2000, 11, 23), "M", "Mécanique", "CDD", 225m, null, "av,sergent moke n°353 C/gombe", new DateTime(2025, 3, 5)),
        new("TMP-0028", "LOWO", "MWEMBO", "Isaac", new DateTime(2000, 1, 19), "M", "Mécanique", "CDD", 275m, null, "av, mudjala n°59 C/ limete ", new DateTime(2025, 3, 5)),
        new("TMP-0029", "KANANDA", "KALEKA", "Papy", new DateTime(1978, 2, 21), "M", "Mécanique", "CDD", 225m, null, "av,kinzazi n°8B, C/matete", new DateTime(2024, 10, 5)),
        new("TMP-0030", "NKOKGOLO", "JOSEPH", "Nelly", new DateTime(1992, 7, 31), "M", "Mécanique", "CDD", 225m, null, "av, isoke n°A14 C/barubu", new DateTime(2025, 10, 26)),
        new("TMP-0031", "MABALAMENE", "MABILAMENE", "Moise", new DateTime(1991, 11, 23), "M", "Mécanique", "CDD", 125m, null, "av, feshi n° 32 C/bumbu", new DateTime(2025, 10, 5)),
        new("TMP-0043", "MBUBITU", null, "Rachidi", new DateTime(1995, 12, 31), "M", "Peinture", "CDI", 167m, null, "av, tuwisana n°143, C/bubu", new DateTime(2024, 10, 5)),
        new("TMP-0044", "NTITBA", "ADAMO", "Ada", new DateTime(1999, 10, 10), "M", "Peinture", "CDD", 105m, null, "av, kintsaku n° 13, C/ bumbu ", new DateTime(2025, 2, 25)),
        new("TMP-0045", "MAFA", "GEORGE", "Ismael", new DateTime(2009, 3, 26), "M", "Peinture", "Stage", 75m, null, "av, ndindi n° 42, C/ ngaliema ", new DateTime(2025, 11, 6)),
        new("TMP-0046", "BIOI", "IMONGO", "Eveline", null, "F", "Peinture", "CDI", 75m, null, null, new DateTime(2024, 1, 8)),
        new("TMP-0049", "WAZALANGA", null, "Jonathan", null, "M", "Residence", "CDD", 75m, null, null, new DateTime(2025, 1, 1))
    };

    /// <summary>
    /// Insère les employés manquants et leurs contrats (STE LTSERVICES).
    /// </summary>
    public static void SeedEffectifsSiVide(PaieDbContext db)
    {
        var etab = db.Etablissements.AsNoTracking().FirstOrDefault();
        if (etab == null)
            return;

        var depts = db.Departements
            .AsNoTracking()
            .Where(d => d.EtablissementId == etab.Id)
            .ToDictionary(d => d.NomDepartement, d => d.Id, StringComparer.Ordinal);

        var catExec = db.CategoriesProfessionnelles.AsNoTracking().FirstOrDefault(c => c.Libelle == "Exécution");
        if (catExec == null)
            return;

        foreach (var ligne in Effectifs)
        {
            if (!depts.TryGetValue(ligne.Departement, out var depId))
                continue;
            if (db.Employes.Any(e => e.Matricule == ligne.Matricule))
                continue;

            var emp = new Employe
            {
                Matricule = ligne.Matricule,
                Nom = ligne.Nom,
                Postnom = ligne.Postnom,
                Prenom = ligne.Prenom,
                DateNaissance = ligne.DateNaissance,
                Sexe = ligne.Sexe,
                Adresse = ligne.Adresse,
                NumCnss = ligne.NumCnss,
                DepartementId = depId
            };
            db.Employes.Add(emp);
            db.SaveChanges();

            db.Contrats.Add(new Contrat
            {
                EmployeId = emp.Id,
                TypeContrat = ligne.TypeContrat,
                DateDebut = ligne.DateDebutContrat,
                DateFin = null,
                SalaireBase = decimal.Round(ligne.SalaireBase, 2, MidpointRounding.AwayFromZero),
                DeviseBase = "USD",
                CategorieProfessionnelleId = catExec.Id,
                TauxMajorationHeuresSup = 50,
                TauxMajorationNuit = 0,
                TauxMajorationJourFerie = 0,
                PreavisMoisBase = 0,
                IndemniteLicenciementMoisBase = 0
            });
            db.SaveChanges();
        }
    }
}
