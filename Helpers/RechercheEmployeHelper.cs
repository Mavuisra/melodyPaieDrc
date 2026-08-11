using System.Globalization;
using System.Text;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Helpers;

/// <summary>Recherche multi-critères sur la liste employés (tokens AND, accents ignorés).</summary>
public static class RechercheEmployeHelper
{
    public static IEnumerable<Employe> Filtrer(IEnumerable<Employe> source, string? requete)
    {
        var tokens = ExtraireTokens(requete);
        if (tokens.Count == 0)
            return source;

        return source.Where(e => Correspond(e, tokens));
    }

    /// <summary>Top suggestions triées par pertinence (matricule, nom, département…).</summary>
    public static IReadOnlyList<Employe> Suggerer(IEnumerable<Employe> source, string? requete, int max = 8)
    {
        var tokens = ExtraireTokens(requete);
        if (tokens.Count == 0 || max <= 0)
            return Array.Empty<Employe>();

        return source
            .Select(e => (Employe: e, Score: CalculerScore(e, tokens)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Employe.Nom, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Employe.Prenom, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .Select(x => x.Employe)
            .ToList();
    }

    public static bool Correspond(Employe employe, string? requete) =>
        Correspond(employe, ExtraireTokens(requete));

    private static bool Correspond(Employe e, IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0) return true;

        var champs = ConstruireChampsRecherche(e);
        return tokens.All(token => champs.Any(c => c.Contains(token, StringComparison.Ordinal)));
    }

    private static List<string> ExtraireTokens(string? requete)
    {
        if (string.IsNullOrWhiteSpace(requete))
            return new List<string>();

        return Normaliser(requete)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToList();
    }

    private static List<string> ConstruireChampsRecherche(Employe e)
    {
        var nomComplet = $"{e.Nom} {e.Postnom} {e.Prenom}".Trim();
        var ordreInverse = $"{e.Prenom} {e.Nom} {e.Postnom}".Trim();
        var initiales = ConstruireInitiales(e.Nom, e.Postnom, e.Prenom);

        var champs = new List<string>
        {
            Normaliser(e.Matricule),
            Normaliser(nomComplet),
            Normaliser(ordreInverse),
            Normaliser(e.Nom),
            Normaliser(e.Postnom),
            Normaliser(e.Prenom),
            Normaliser(initiales),
            Normaliser(e.Departement?.NomDepartement),
            Normaliser(e.Telephone),
            NormaliserChiffres(e.Telephone),
            Normaliser(e.ZkUserId),
            Normaliser(e.NumCnss),
            Normaliser(e.CommuneAffectation),
            Normaliser(e.Adresse),
            Normaliser(e.Sexe),
            Normaliser(e.EtatCivil),
            Normaliser(e.LibelleBanque),
            Normaliser(e.CodeBanque),
            Normaliser(e.TitulaireCompteBancaire),
            Normaliser(e.NumeroCompteBancaire),
            Normaliser(e.SalaireMensuelUsd.ToString("N2", CultureInfo.InvariantCulture)),
            Normaliser(e.SalaireMensuelCdf.ToString("N0", CultureInfo.InvariantCulture))
        };

        if (e.DateNaissance.HasValue)
        {
            var d = e.DateNaissance.Value;
            champs.Add(Normaliser(d.ToString("dd/MM/yyyy")));
            champs.Add(Normaliser(d.ToString("yyyy")));
        }

        return champs.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
    }

    private static int CalculerScore(Employe e, IReadOnlyList<string> tokens)
    {
        if (!Correspond(e, tokens))
            return 0;

        var matricule = Normaliser(e.Matricule);
        var nom = Normaliser(e.Nom);
        var prenom = Normaliser(e.Prenom);
        var postnom = Normaliser(e.Postnom);
        var nomComplet = Normaliser($"{e.Nom} {e.Postnom} {e.Prenom}".Trim());
        var departement = Normaliser(e.Departement?.NomDepartement);
        var telephone = NormaliserChiffres(e.Telephone);
        var cnss = Normaliser(e.NumCnss);

        var score = 0;
        foreach (var token in tokens)
        {
            if (matricule == token) score += 200;
            else if (matricule.StartsWith(token, StringComparison.Ordinal)) score += 140;
            else if (matricule.Contains(token, StringComparison.Ordinal)) score += 70;

            if (nom == token || prenom == token) score += 120;
            else if (nomComplet.StartsWith(token, StringComparison.Ordinal)) score += 100;
            else if (nom.StartsWith(token, StringComparison.Ordinal) || prenom.StartsWith(token, StringComparison.Ordinal)) score += 90;
            else if (postnom.StartsWith(token, StringComparison.Ordinal)) score += 60;
            else if (nomComplet.Contains(token, StringComparison.Ordinal)) score += 40;

            if (departement.StartsWith(token, StringComparison.Ordinal)) score += 50;
            else if (departement.Contains(token, StringComparison.Ordinal)) score += 25;

            if (!string.IsNullOrEmpty(telephone) && telephone.Contains(token, StringComparison.Ordinal)) score += 45;
            if (cnss.Contains(token, StringComparison.Ordinal)) score += 55;
        }

        return score;
    }

    private static string ConstruireInitiales(string? nom, string? postnom, string? prenom)
    {
        static char? PremièreLettre(string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : char.ToUpperInvariant(s.Trim()[0]);

        var lettres = new[] { PremièreLettre(prenom), PremièreLettre(nom), PremièreLettre(postnom) }
            .Where(c => c.HasValue)
            .Select(c => c!.Value);
        return new string(lettres.ToArray());
    }

    private static string NormaliserChiffres(string? valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur)) return "";
        var sb = new StringBuilder(valeur.Length);
        foreach (var c in valeur)
        {
            if (char.IsDigit(c))
                sb.Append(c);
        }
        return sb.ToString();
    }

    public static string Normaliser(string? valeur)
    {
        if (string.IsNullOrWhiteSpace(valeur))
            return "";

        var formD = valeur.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
