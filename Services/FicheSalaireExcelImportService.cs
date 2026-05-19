using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Import des lignes d'une grille paie Excel (feuille « SALAIRE ET TAXE », format type LTSERVICES) : employés + contrats + affectations primes/indemnités. Ne remplace pas une restauration .db.
/// </summary>
public sealed class FicheSalaireExcelImportResult
{
    public int EmployesCrees { get; set; }
    public int LignesIgnorees { get; set; }
    /// <summary>Nombre de lignes d'affectation primes/indemnités créées (transport, logement, etc.).</summary>
    public int AffectationsCreees { get; set; }
    public List<string> Messages { get; } = new();
}

public static class FicheSalaireExcelImportService
{
    private const decimal NombreJoursPaieDefaut = 26m;

    public static FicheSalaireExcelImportResult Importer(string cheminFichier, PaieDbContext db)
    {
        var result = new FicheSalaireExcelImportResult();
        if (string.IsNullOrWhiteSpace(cheminFichier) || !File.Exists(cheminFichier))
            throw new FileNotFoundException("Fichier Excel introuvable.", cheminFichier);

        using var wb = new XLWorkbook(cheminFichier);
        var ws = wb.Worksheets.FirstOrDefault(w => w.Name.Trim().Equals("SALAIRE ET TAXE", StringComparison.OrdinalIgnoreCase))
                 ?? wb.Worksheets.First();

        var headerRow = TrouverLigneEntete(ws);
        if (headerRow < 0)
            throw new InvalidOperationException("En-têtes attendus introuvables (colonne « NOM » / « Salaire mensuel en FC »).");

        var colNbre = TrouverColonne(ws, headerRow, "Nbre", "N°");
        var colNom = TrouverColonne(ws, headerRow, "NOM & POST", "NOM");
        var colFonction = TrouverColonne(ws, headerRow, "FONCTION");
        var colCategorie = TrouverColonne(ws, headerRow, "CATEGORIE");
        var colSmig = TrouverColonne(ws, headerRow, "SMIG 2025", "SMIG");
        var colSalaireMensuel = TrouverColonne(ws, headerRow, "Salaire mensuel en FC", "Salaire mensuel");

        // Colonnes optionnelles — décomposition jour → mois (grille LTS)
        var colSalBaseJr = TrouverColonne(ws, headerRow, "SAL BASE/Jr", "SAL BASE", "SALAIRE BASE/Jr");
        var colAnnuiteJr = TrouverColonne(ws, headerRow, "ANNUITE/Jr", "ANNUITE");
        var colTransportJr = TrouverColonne(ws, headerRow, "TRANSPORT/JOUR", "TRANSPORT");
        var colLogementJr = TrouverColonne(ws, headerRow, "LOGEMENT/JOUR", "LOGEMENT");
        var colAlfaJr = TrouverColonne(ws, headerRow, "ALFA/JOUR", "ALFA");
        var colNbreJr = TrouverColonne(ws, headerRow, "Nbre de Jr", "Nbre Jr", "Nbre de jour");
        var colTensionJr = TrouverColonne(ws, headerRow, "TENSION");
        var colBrutImposable = TrouverColonne(ws, headerRow, "Brut imposable CNSS", "Brut imposable");
        var colIpr = TrouverColonne(ws, headerRow, "IPR   15", "IPR  15", "IPR");
        var colCnss = TrouverColonne(ws, headerRow, "CNSS   18", "CNSS  18");
        var colInpp = TrouverColonne(ws, headerRow, "INPP  3", "INPP");

        if (colNom < 0 || colSalaireMensuel < 0)
            throw new InvalidOperationException("Colonnes obligatoires manquantes (nom ou salaire mensuel FC).");

        var decompositionLts = colSalBaseJr > 0;

        var etab = db.Etablissements.AsNoTracking().FirstOrDefault()
                   ?? throw new InvalidOperationException("Aucun établissement : exécutez d'abord l'initialisation de la base.");
        var depts = db.Departements.AsNoTracking()
            .Where(d => d.EtablissementId == etab.Id)
            .ToDictionary(d => d.NomDepartement, d => d.Id, StringComparer.OrdinalIgnoreCase);

        var catCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var primesCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? headerRow;
        for (var r = headerRow + 1; r <= lastRow; r++)
        {
            var cellNbre = colNbre > 0 ? ws.Cell(r, colNbre) : ws.Cell(r, 1);
            if (cellNbre.IsEmpty() || string.IsNullOrWhiteSpace(LireTexteCellule(cellNbre)))
                break;

            if (!decimal.TryParse(LireTexteCellule(cellNbre), NumberStyles.Any, CultureInfo.InvariantCulture, out var nOrdre))
                break;

            var nomComplet = colNom > 0 ? LireTexteCellule(ws.Cell(r, colNom)).Trim() : "";
            if (string.IsNullOrWhiteSpace(nomComplet))
            {
                result.LignesIgnorees++;
                continue;
            }

            var fonction = colFonction > 0 ? LireTexteCellule(ws.Cell(r, colFonction)).Trim() : "";
            var codeCat = colCategorie > 0 ? LireTexteCellule(ws.Cell(r, colCategorie)).Trim() : "DEF";
            if (string.IsNullOrWhiteSpace(codeCat)) codeCat = "DEF";

            var smig = colSmig > 0 ? (LireDecimalCellule(ws.Cell(r, colSmig)) ?? 0m) : 0m;

            var salaireMensuel = LireDecimalCellule(ws.Cell(r, colSalaireMensuel));
            if (salaireMensuel is null or <= 0)
            {
                result.LignesIgnorees++;
                result.Messages.Add($"Ligne {r} : salaire mensuel FC manquant ou nul — {nomComplet}");
                continue;
            }

            var matricule = $"LTS-{(int)nOrdre:D3}";
            if (db.Employes.Any(e => e.Matricule == matricule))
            {
                result.LignesIgnorees++;
                continue;
            }

            ParseNomComplet(nomComplet, out var nom, out var postnom, out var prenom);

            var nomDept = ResoudreDepartement(fonction);
            if (!depts.TryGetValue(nomDept, out var depId))
            {
                result.Messages.Add($"Ligne {r} : département « {nomDept} » introuvable — {nomComplet}");
                result.LignesIgnorees++;
                continue;
            }

            var catId = ObtenirCategorieId(db, catCache, codeCat, smig);

            var emp = new Employe
            {
                Matricule = matricule,
                Nom = nom,
                Postnom = string.IsNullOrWhiteSpace(postnom) ? "." : postnom,
                Prenom = string.IsNullOrWhiteSpace(prenom) ? "." : prenom,
                Sexe = "M",
                EtatCivil = "Célibataire",
                DateNaissance = new DateTime(1990, 1, 1),
                Telephone = "000000000",
                NumCnss = $"LTS-TEST-{(int)nOrdre:D4}",
                Adresse = "Kinshasa (import fiche salaire)",
                DepartementId = depId
            };
            db.Employes.Add(emp);

            if (colBrutImposable > 0)
            {
                var br = LireDecimalCellule(ws.Cell(r, colBrutImposable));
                if (br is > 0) emp.ReferenceBrutImposableCnssCdf = decimal.Round(br.Value, 2, MidpointRounding.AwayFromZero);
            }

            if (colIpr > 0)
            {
                var ipr = LireDecimalCellule(ws.Cell(r, colIpr));
                if (ipr is > 0) emp.ReferenceIprNetCdf = decimal.Round(ipr.Value, 2, MidpointRounding.AwayFromZero);
            }

            if (colCnss > 0)
            {
                var cn = LireDecimalCellule(ws.Cell(r, colCnss));
                if (cn is decimal cnv && cnv >= 0)
                    emp.ReferenceCnssOuvrierCdf = decimal.Round(cnv, 2, MidpointRounding.AwayFromZero);
            }

            if (colInpp > 0)
            {
                var inp = LireDecimalCellule(ws.Cell(r, colInpp));
                if (inp is decimal inpv && inpv >= 0)
                    emp.ReferenceInppCdf = decimal.Round(inpv, 2, MidpointRounding.AwayFromZero);
            }

            db.SaveChanges();

            decimal salaireBaseContrat;

            if (decompositionLts)
            {
                var nbrJours = colNbreJr > 0
                    ? LireDecimalCellule(ws.Cell(r, colNbreJr)) ?? NombreJoursPaieDefaut
                    : NombreJoursPaieDefaut;
                if (nbrJours <= 0) nbrJours = NombreJoursPaieDefaut;

                var tensionJr = colTensionJr > 0 ? LireDecimalCellule(ws.Cell(r, colTensionJr)) ?? 0m : 0m;
                var salBaseJr = colSalBaseJr > 0 ? LireDecimalCellule(ws.Cell(r, colSalBaseJr)) ?? 0m : 0m;
                var annuiteJr = colAnnuiteJr > 0 ? LireDecimalCellule(ws.Cell(r, colAnnuiteJr)) ?? 0m : 0m;
                var transportJr = colTransportJr > 0 ? LireDecimalCellule(ws.Cell(r, colTransportJr)) ?? 0m : 0m;
                var logementJr = colLogementJr > 0 ? LireDecimalCellule(ws.Cell(r, colLogementJr)) ?? 0m : 0m;
                var alfaJr = colAlfaJr > 0 ? LireDecimalCellule(ws.Cell(r, colAlfaJr)) ?? 0m : 0m;

                var tensionM = decimal.Round(tensionJr * nbrJours, 2, MidpointRounding.AwayFromZero);
                var baseM = decimal.Round(salBaseJr * nbrJours, 2, MidpointRounding.AwayFromZero);
                var annuiteM = decimal.Round(annuiteJr * nbrJours, 2, MidpointRounding.AwayFromZero);
                var transportM = decimal.Round(transportJr * nbrJours, 2, MidpointRounding.AwayFromZero);
                var logementM = decimal.Round(logementJr * nbrJours, 2, MidpointRounding.AwayFromZero);
                var alfaM = decimal.Round(alfaJr * nbrJours, 2, MidpointRounding.AwayFromZero);

                var sommeParties = tensionM + baseM + annuiteM + transportM + logementM + alfaM;
                if (Math.Abs(sommeParties - salaireMensuel.Value) > 5m)
                    result.Messages.Add(
                        $"Ligne {r} ({nomComplet}) : écart somme composantes / salaire mensuel FC ≈ {decimal.Round(sommeParties - salaireMensuel.Value, 2)} — vérifier le fichier.");

                if (baseM > 0)
                    salaireBaseContrat = baseM;
                else
                    salaireBaseContrat = decimal.Round(salaireMensuel.Value, 2, MidpointRounding.AwayFromZero);

                void AjouterAffectation(decimal montant, string clePrime)
                {
                    if (montant <= 0) return;
                    var pid = ObtenirPrimeIndemniteId(db, primesCache, clePrime);
                    db.AffectationsPrimesIndemnites.Add(new AffectationPrimeIndemnite
                    {
                        EmployeId = emp.Id,
                        PrimeIndemniteId = pid,
                        Montant = montant
                    });
                    result.AffectationsCreees++;
                }

                AjouterAffectation(tensionM, "Tension");
                AjouterAffectation(annuiteM, "Annuité");
                AjouterAffectation(transportM, "Indemnité de transport");
                AjouterAffectation(logementM, "Indemnité de logement");
                AjouterAffectation(alfaM, "Allocations familiales");

                db.SaveChanges();
            }
            else
            {
                salaireBaseContrat = decimal.Round(salaireMensuel.Value, 2, MidpointRounding.AwayFromZero);
            }

            db.Contrats.Add(new Contrat
            {
                EmployeId = emp.Id,
                TypeContrat = "CDI",
                DateDebut = new DateTime(2025, 1, 1),
                DateFin = null,
                SalaireBase = salaireBaseContrat,
                DeviseBase = "CDF",
                CategorieProfessionnelleId = catId,
                TauxMajorationHeuresSup = 50m,
                TauxMajorationNuit = 30m,
                TauxMajorationJourFerie = 100m,
                PreavisMoisBase = 1m,
                IndemniteLicenciementMoisBase = 0m
            });
            db.SaveChanges();
            result.EmployesCrees++;
        }

        if (result.EmployesCrees > 0)
        {
            result.Messages.Insert(0,
                $"{result.EmployesCrees} employé(s), contrat(s) CDF et affectations (transport / logement / etc.) selon les colonnes disponibles.");
        }

        return result;
    }

    /// <summary>Résout une prime par libellé référent ou par libellé court (clé).</summary>
    private static int ObtenirPrimeIndemniteId(PaieDbContext db, Dictionary<string, int> cache, string cleOuLibelle)
    {
        if (cache.TryGetValue(cleOuLibelle, out var id))
            return id;

        var p = db.PrimesIndemnites.FirstOrDefault(x =>
            x.Libelle.Equals(cleOuLibelle, StringComparison.OrdinalIgnoreCase));

        if (p != null)
        {
            cache[cleOuLibelle] = p.Id;
            return p.Id;
        }

        var (libelle, imp, cotis) = cleOuLibelle switch
        {
            "Tension" => ("Tension", true, true),
            "Annuité" => ("Annuité", true, true),
            "Indemnité de transport" => ("Indemnité de transport", false, false),
            "Indemnité de logement" => ("Indemnité de logement", true, true),
            "Allocations familiales" => ("Allocations familiales", false, false),
            _ => (cleOuLibelle, true, true)
        };

        var nouvelle = new PrimeIndemnite
        {
            Libelle = libelle,
            EstImposable = imp,
            EstCotisable = cotis,
            ModeCalcul = PrimeIndemnite.ModeFixe,
            TypeLigne = PrimeIndemnite.TypeAvantage
        };
        db.PrimesIndemnites.Add(nouvelle);
        db.SaveChanges();
        cache[cleOuLibelle] = nouvelle.Id;
        return nouvelle.Id;
    }

    private static int TrouverLigneEntete(IXLWorksheet ws)
    {
        for (var r = 1; r <= 25; r++)
        {
            for (var c = 1; c <= 30; c++)
            {
                var t = ws.Cell(r, c).GetString();
                if (t.Contains("Salaire mensuel", StringComparison.OrdinalIgnoreCase) &&
                    t.Contains("FC", StringComparison.OrdinalIgnoreCase))
                    return r;
            }
        }

        return -1;
    }

    /// <summary>Retourne l'index de colonne (1-based) ou -1.</summary>
    private static int TrouverColonne(IXLWorksheet ws, int headerRow, params string[] cles)
    {
        for (var c = 1; c <= 36; c++)
        {
            var h = ws.Cell(headerRow, c).GetString().Trim();
            foreach (var k in cles)
            {
                if (k.Length == 0) continue;
                if (h.Contains(k, StringComparison.OrdinalIgnoreCase))
                    return c;
            }
        }

        return -1;
    }

    private static string LireTexteCellule(IXLCell cell)
    {
        try
        {
            return cell.GetString();
        }
        catch
        {
            return cell.CachedValue.ToString();
        }
    }

    /// <summary>Lecture valeur numérique sans forcer le recalcul des formules (références Excel non supportées).</summary>
    private static decimal? LireDecimalCellule(IXLCell cell)
    {
        try
        {
            if (cell.HasFormula)
                return LireDecimal(cell.CachedValue);
            return LireDecimal(cell.Value);
        }
        catch
        {
            try
            {
                return LireDecimal(cell.CachedValue);
            }
            catch
            {
                return null;
            }
        }
    }

    private static decimal? LireDecimal(object? v)
    {
        if (v == null) return null;
        if (v is XLCellValue xcv)
        {
            try
            {
                if (xcv.IsBlank) return null;
            }
            catch
            {
                /* XLCellValue API */
            }
            return LireDecimal(xcv.ToString());
        }

        if (v is decimal d) return d;
        if (v is double db) return (decimal)db;
        if (v is float f) return (decimal)f;
        if (v is int i) return i;
        var s = v.ToString()?.Trim();
        if (string.IsNullOrEmpty(s)) return null;

        const NumberStyles sty = NumberStyles.Any;
        if (decimal.TryParse(s, sty, CultureInfo.GetCultureInfo("fr-FR"), out var fr))
            return fr;
        if (decimal.TryParse(s, sty, CultureInfo.InvariantCulture, out var inv))
            return inv;
        return null;
    }

    private static void ParseNomComplet(string raw, out string nom, out string? postnom, out string? prenom)
    {
        nom = "INCONNU";
        postnom = null;
        prenom = null;
        if (string.IsNullOrWhiteSpace(raw)) return;

        var parts = raw.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;
        nom = parts[0].Trim();
        if (parts.Length == 1) return;
        if (parts.Length == 2)
        {
            prenom = parts[1];
            return;
        }

        postnom = parts[1];
        prenom = string.Join(" ", parts.Skip(2));
    }

    private static string ResoudreDepartement(string? fonction)
    {
        if (string.IsNullOrWhiteSpace(fonction)) return "Mécanique";
        var f = fonction.ToUpperInvariant();
        if (f.Contains("GERANT") || f.Contains("GÉRANT")) return "Administration";
        if (f.Contains("NETTOY")) return "Nettoyage";
        if (f.Contains("CHAUFF")) return "Chauffeur";
        if (f.Contains("MAGASIN")) return "Magasin";
        if (f.Contains("ELECTRI") || f.Contains("CLIMAT") || f.Contains("ÉLECTRI")) return "Electricité - Climatisation";
        if (f.Contains("MECANO")) return "Mécanique";
        if (f.Contains("TOLIER")) return "Tolerie";
        if (f.Contains("PEINTRE") || f.Contains("PONCEUR")) return "Peinture";
        if (f.Contains("RESIDEN") || f.Contains("JARDIN")) return "Residence";
        if (f.Contains("AJUST")) return "Ajustage";
        return "Mécanique";
    }

    private static int ObtenirCategorieId(PaieDbContext db, Dictionary<string, int> cache, string codeCat, decimal smig)
    {
        var cle = codeCat.Trim().ToUpperInvariant();
        if (cache.TryGetValue(cle, out var id))
            return id;

        var libelle = $"Fiche LTS {codeCat.Trim()}";
        var existant = db.CategoriesProfessionnelles.FirstOrDefault(c => c.Libelle == libelle);
        if (existant != null)
        {
            cache[cle] = existant.Id;
            return existant.Id;
        }

        var smigMensuel = smig > 0 ? smig * 26m : 377000m;
        var cat = new CategorieProfessionnelle
        {
            Libelle = libelle.Length > 100 ? libelle[..100] : libelle,
            SmigApplique = decimal.Round(smigMensuel, 2, MidpointRounding.AwayFromZero)
        };
        db.CategoriesProfessionnelles.Add(cat);
        db.SaveChanges();
        cache[cle] = cat.Id;
        return cat.Id;
    }
}
