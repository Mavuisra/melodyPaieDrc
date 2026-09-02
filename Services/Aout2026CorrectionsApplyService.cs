using System.IO;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Applique les corrections paie Août 2026 validées sans toucher aux présences (SuivisJournaliers, JoursPrestes).
/// </summary>
public static class Aout2026CorrectionsApplyService
{
    public sealed class Resultat
    {
        public int EmployesTraites { get; init; }
        public int EmployesIgnores { get; init; }
        public int AffectationsKm { get; init; }
        public int AffectationsLogement { get; init; }
        public int SaisiesModifiees { get; init; }
        public int QuinzainesModifiees { get; init; }
        public int StagiairesSalaire { get; init; }
        public int BulletinsRegeneres { get; init; }
        public int BulletinsConformes { get; init; }
        public string CheminBackup { get; set; } = "";
        public List<string> Avertissements { get; init; } = [];

        public string Resume =>
            $"{EmployesTraites} employé(s) corrigé(s) — KM: {AffectationsKm}, logement: {AffectationsLogement}, " +
            $"saisies: {SaisiesModifiees}, quinzaines: {QuinzainesModifiees}, stagiaires 100 $: {StagiairesSalaire}. " +
            $"Bulletins: {BulletinsConformes}/{BulletinsRegeneres} conformes (NET ±{ToleranceNetUsd} $).";
    }

    public const decimal ToleranceNetUsd = 0.50m;

    public static bool EstPeriodeCible(PeriodePaie? periode)
        => periode is { Mois: Aout2026CorrectionsCatalog.MoisCible, Annee: Aout2026CorrectionsCatalog.AnneeCible };

    /// <summary>Sauvegarde automatique avant patch (dossier Data).</summary>
    public static string CreerBackupAutomatique()
    {
        var dbPath = PaieDbContext.GetDatabasePath();
        if (!File.Exists(dbPath))
            throw new FileNotFoundException("Base de données introuvable.", dbPath);

        if (!DatabaseBackupService.EstIntegriteValide(dbPath))
            throw new InvalidOperationException("La base actuelle est endommagée ; sauvegarde annulée.");

        var backupPath = Path.Combine(
            PaieDbContext.GetDataDirectory(),
            $"PaieRDC_avant_corrections_aout2026_{DateTime.Now:yyyyMMdd_HHmmss}.db");

        DatabaseBackupService.AssurerSchemaAvantSauvegarde();
        DatabaseBackupService.ExporterCopieCoherente(dbPath, backupPath);

        var verification = DatabaseBackupService.ValiderFichierBackup(backupPath);
        if (!verification.EstValide)
            throw new InvalidOperationException($"Backup créé mais invalide : {verification.MessageErreur}");

        return backupPath;
    }

    public static Resultat Appliquer(PaieDbContext db, int periodePaieId)
    {
        var periode = db.PeriodesPaie.FirstOrDefault(p => p.Id == periodePaieId)
                      ?? throw new InvalidOperationException("Période introuvable.");

        if (!EstPeriodeCible(periode))
            throw new InvalidOperationException(
                $"Cette action ne s'applique qu'à Août {Aout2026CorrectionsCatalog.AnneeCible} " +
                $"(période sélectionnée : {periode.Mois:D2}/{periode.Annee}).");

        if (periode.Cloturee)
            throw new InvalidOperationException("La période est clôturée. Déclôturez-la avant d'appliquer les corrections.");

        var primeKmId = TrouverPrimeKmId(db);
        var primeLogId = TrouverPrimeLogementId(db);
        var avertissements = new List<string>();
        var result = new Resultat { Avertissements = avertissements };

        if (primeKmId == null)
            avertissements.Add("Rubrique indemnité KM introuvable — les montants KM n'ont pas été appliqués.");
        if (primeLogId == null)
            avertissements.Add("Rubrique indemnité logement introuvable — les montants logement n'ont pas été appliqués.");

        var employeIds = db.Employes.Select(e => e.Id).ToHashSet();
        var politique = new PolitiquePaieService(db).Charger(
            ContexteEntrepriseService.ObtenirEntrepriseCouranteId(db));
        var (dateDebut, dateFin) = PeriodePaieHelper.ObtenirBornes(periode, politique);

        var affectKm = 0;
        var affectLog = 0;
        var saisies = 0;
        var quinz = 0;
        var stag = 0;
        var traites = 0;
        var ignores = 0;

        foreach (var ligne in Aout2026CorrectionsCatalog.Lignes)
        {
            if (!employeIds.Contains(ligne.EmployeId))
            {
                ignores++;
                avertissements.Add($"Employé Id={ligne.EmployeId} introuvable — ligne ignorée.");
                continue;
            }

            if (Aout2026CorrectionsCatalog.StagiaireEmployeIds.Contains(ligne.EmployeId))
            {
                if (MettreAJourSalaireStagiaire(db, ligne.EmployeId, dateDebut, dateFin))
                    stag++;
            }

            if (ligne.Km.HasValue && primeKmId != null)
            {
                UpsertAffectation(db, ligne.EmployeId, primeKmId.Value, ligne.Km.Value);
                affectKm++;
            }

            if (ligne.Logement.HasValue && primeLogId != null)
            {
                UpsertAffectation(db, ligne.EmployeId, primeLogId.Value, ligne.Logement.Value);
                affectLog++;
            }

            if (MettreAJourSaisiePaie(db, periodePaieId, ligne))
                saisies++;

            if (AssurerSaisieConformeBulletin(db, periodePaieId, ligne))
                saisies++;

            if (DefinirQuinzaine(db, periodePaieId, ligne))
                quinz++;

            traites++;
        }

        QuinzaineOctroiService.SynchroniserAcomptesPeriodePourTous(db, periodePaieId);
        db.SaveChanges();

        var (regeneres, conformes, erreursBulletin) = RegenererEtConformerBulletins(db, periodePaieId);
        avertissements.AddRange(erreursBulletin);

        return new Resultat
        {
            EmployesTraites = traites,
            EmployesIgnores = ignores,
            AffectationsKm = affectKm,
            AffectationsLogement = affectLog,
            SaisiesModifiees = saisies,
            QuinzainesModifiees = quinz,
            StagiairesSalaire = stag,
            BulletinsRegeneres = regeneres,
            BulletinsConformes = conformes,
            Avertissements = avertissements
        };
    }

    /// <summary>Supprime et regénère les bulletins corrigés, puis ajuste le NET cible.</summary>
    public static (int Regeneres, int Conformes, List<string> Erreurs) RegenererEtConformerBulletins(
        PaieDbContext db, int periodePaieId)
    {
        var employeIds = Aout2026CorrectionsCatalog.Lignes.Select(l => l.EmployeId).ToHashSet();
        var cibles = Aout2026CorrectionsCatalog.Lignes
            .Where(l => db.Employes.Any(e => e.Id == l.EmployeId))
            .ToList();
        var erreurs = new List<string>();

        var anciens = db.BulletinsPaie
            .Include(b => b.Details)
            .Where(b => b.PeriodePaieId == periodePaieId && employeIds.Contains(b.EmployeId))
            .ToList();
        foreach (var bulletin in anciens)
        {
            db.BulletinsDetails.RemoveRange(bulletin.Details);
            db.BulletinsPaie.Remove(bulletin);
        }
        db.SaveChanges();

        var calc = new CalculPaieService(db);
        var regeneres = 0;
        foreach (var ligne in cibles)
        {
            try
            {
                calc.GenererBulletin(ligne.EmployeId, periodePaieId);
                regeneres++;
            }
            catch (Exception ex)
            {
                erreurs.Add($"Bulletin Id={ligne.EmployeId} : {ex.Message}");
            }
        }

        var conformes = 0;
        foreach (var ligne in cibles)
        {
            try
            {
                if (AjusterNetBulletin(db, calc, periodePaieId, ligne))
                    conformes++;
                else
                    erreurs.Add($"NET Id={ligne.EmployeId} : écart > {ToleranceNetUsd} $ (cible {ligne.NetCibleReference:N2}).");
            }
            catch (Exception ex)
            {
                erreurs.Add($"Ajustement NET Id={ligne.EmployeId} : {ex.Message}");
            }
        }

        db.SaveChanges();
        return (regeneres, conformes, erreurs);
    }

    private static bool AjusterNetBulletin(
        PaieDbContext db,
        CalculPaieService calc,
        int periodePaieId,
        Aout2026CorrectionLigne ligne)
    {
        var bulletin = db.BulletinsPaie
            .FirstOrDefault(b => b.EmployeId == ligne.EmployeId && b.PeriodePaieId == periodePaieId);
        if (bulletin == null)
            return false;

        var delta = bulletin.NetAPayer - ligne.NetCibleReference;
        if (Math.Abs(delta) <= ToleranceNetUsd)
            return true;

        SupprimerBulletin(db, bulletin);

        var saisie = db.SaisiesPaie
            .FirstOrDefault(s => s.EmployeId == ligne.EmployeId && s.PeriodePaieId == periodePaieId);
        if (saisie == null)
            return false;

        var jours = saisie.JoursPrestes;
        saisie.AutresRetenues = 0;
        saisie.AutresGainsNonImposables = 0;
        db.SaveChanges();

        var b1 = calc.GenererBulletin(ligne.EmployeId, periodePaieId);
        delta = b1.NetAPayer - ligne.NetCibleReference;
        if (Math.Abs(delta) <= ToleranceNetUsd)
            return true;

        SupprimerBulletin(db, b1);
        if (delta > 0)
            saisie.AutresRetenues = RoundPaie(delta);
        else
            saisie.AutresGainsNonImposables = RoundPaie(-delta);
        saisie.JoursPrestes = jours;
        db.SaveChanges();

        var b2 = calc.GenererBulletin(ligne.EmployeId, periodePaieId);
        delta = b2.NetAPayer - ligne.NetCibleReference;
        return Math.Abs(delta) <= ToleranceNetUsd;
    }

    private static void SupprimerBulletin(PaieDbContext db, BulletinPaie bulletin)
    {
        var details = db.BulletinsDetails.Where(d => d.BulletinPaieId == bulletin.Id).ToList();
        db.BulletinsDetails.RemoveRange(details);
        db.BulletinsPaie.Remove(bulletin);
        db.SaveChanges();
    }

    /// <summary>Prépare saisie pour bulletin conforme (retenue manuelle, reset ajustements).</summary>
    private static bool AssurerSaisieConformeBulletin(PaieDbContext db, int periodePaieId, Aout2026CorrectionLigne ligne)
    {
        var saisie = db.SaisiesPaie
            .FirstOrDefault(s => s.EmployeId == ligne.EmployeId && s.PeriodePaieId == periodePaieId);
        var modifie = false;

        if (saisie == null)
        {
            saisie = new SaisiePaie
            {
                EmployeId = ligne.EmployeId,
                PeriodePaieId = periodePaieId,
                JoursPrestes = 0
            };
            db.SaisiesPaie.Add(saisie);
            modifie = true;
        }

        var joursAvant = saisie.JoursPrestes;
        var retenuAttendu = ligne.RetenuSalaire ?? 0m;

        if (saisie.SanctionsDisciplinaires != retenuAttendu)
        {
            saisie.SanctionsDisciplinaires = retenuAttendu;
            modifie = true;
        }

        if (ligne.Prime.HasValue && saisie.AutresGainsImposables != ligne.Prime.Value)
        {
            saisie.AutresGainsImposables = ligne.Prime.Value;
            modifie = true;
        }

        if (saisie.AutresRetenues != 0)
        {
            saisie.AutresRetenues = 0;
            modifie = true;
        }

        if (saisie.AutresGainsNonImposables != 0)
        {
            saisie.AutresGainsNonImposables = 0;
            modifie = true;
        }

        saisie.JoursPrestes = joursAvant;
        return modifie;
    }

    private static decimal RoundPaie(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static bool MettreAJourSalaireStagiaire(
        PaieDbContext db, int employeId, DateTime dateDebut, DateTime dateFin)
    {
        var contrat = db.Contrats
            .Where(c => c.EmployeId == employeId &&
                        c.DateDebut <= dateFin &&
                        (c.DateFin == null || c.DateFin >= dateDebut))
            .OrderByDescending(c => c.DateDebut)
            .FirstOrDefault();

        if (contrat == null || !StagiairePaieHelper.EstStagiaire(contrat.TypeContrat))
            return false;

        if (contrat.SalaireBase == Aout2026CorrectionsCatalog.StagiaireSalaireUsd)
            return false;

        contrat.SalaireBase = Aout2026CorrectionsCatalog.StagiaireSalaireUsd;
        return true;
    }

    /// <summary>Met à jour retenu / prime uniquement — ne modifie jamais JoursPrestes.</summary>
    private static bool MettreAJourSaisiePaie(PaieDbContext db, int periodePaieId, Aout2026CorrectionLigne ligne)
    {
        var modifie = false;
        var saisie = db.SaisiesPaie
            .FirstOrDefault(s => s.EmployeId == ligne.EmployeId && s.PeriodePaieId == periodePaieId);

        var toucherRetenu = ligne.RetenuSalaire.HasValue;
        var toucherPrime = ligne.Prime.HasValue;
        if (!toucherRetenu && !toucherPrime)
            return false;

        if (saisie == null)
        {
            saisie = new SaisiePaie
            {
                EmployeId = ligne.EmployeId,
                PeriodePaieId = periodePaieId,
                JoursPrestes = 0
            };
            db.SaisiesPaie.Add(saisie);
            modifie = true;
        }

        var joursAvant = saisie.JoursPrestes;

        if (toucherRetenu && saisie.SanctionsDisciplinaires != ligne.RetenuSalaire!.Value)
        {
            saisie.SanctionsDisciplinaires = ligne.RetenuSalaire.Value;
            modifie = true;
        }

        if (toucherPrime && saisie.AutresGainsImposables != ligne.Prime!.Value)
        {
            saisie.AutresGainsImposables = ligne.Prime.Value;
            modifie = true;
        }

        saisie.JoursPrestes = joursAvant;
        return modifie;
    }

    private static bool DefinirQuinzaine(PaieDbContext db, int periodePaieId, Aout2026CorrectionLigne ligne)
    {
        var existants = db.QuinzaineOctrois
            .Where(q => q.EmployeId == ligne.EmployeId && q.PeriodePaieId == periodePaieId)
            .ToList();

        if (ligne.Quinzaine <= 0)
        {
            if (existants.Count == 0)
                return false;
            db.QuinzaineOctrois.RemoveRange(existants);
            return true;
        }

        if (existants.Count == 1 &&
            existants[0].Montant == ligne.Quinzaine &&
            string.Equals(existants[0].Commentaire, "Correction Août 2026", StringComparison.Ordinal))
            return false;

        db.QuinzaineOctrois.RemoveRange(existants);
        db.QuinzaineOctrois.Add(new QuinzaineOctroi
        {
            EmployeId = ligne.EmployeId,
            PeriodePaieId = periodePaieId,
            DateOctroi = new DateTime(Aout2026CorrectionsCatalog.AnneeCible, Aout2026CorrectionsCatalog.MoisCible, 15),
            Montant = ligne.Quinzaine,
            Commentaire = "Correction Août 2026"
        });
        return true;
    }

    private static void UpsertAffectation(PaieDbContext db, int employeId, int primeId, decimal montant)
    {
        var prime = db.PrimesIndemnites.FirstOrDefault(p => p.Id == primeId);
        if (prime != null)
        {
            var doublons = db.AffectationsPrimesIndemnites
                .Where(a => a.EmployeId == employeId && a.PrimeIndemniteId != primeId)
                .Include(a => a.PrimeIndemnite)
                .AsEnumerable()
                .Where(a => a.PrimeIndemnite != null && MemeFamilleIndemnite(prime.Libelle, a.PrimeIndemnite.Libelle))
                .ToList();
            if (doublons.Count > 0)
                db.AffectationsPrimesIndemnites.RemoveRange(doublons);
        }

        var aff = db.AffectationsPrimesIndemnites
            .FirstOrDefault(a => a.EmployeId == employeId && a.PrimeIndemniteId == primeId);

        if (aff == null)
        {
            db.AffectationsPrimesIndemnites.Add(new AffectationPrimeIndemnite
            {
                EmployeId = employeId,
                PrimeIndemniteId = primeId,
                Montant = montant
            });
            return;
        }

        aff.Montant = montant;
    }

    private static bool MemeFamilleIndemnite(string libelleReference, string libelleAutre)
    {
        static bool EstKm(string? l)
        {
            if (string.IsNullOrWhiteSpace(l)) return false;
            var u = l.ToUpperInvariant();
            return u.Contains("KM", StringComparison.Ordinal) ||
                   u.Contains("KILOM", StringComparison.Ordinal) ||
                   u.Contains("DEPLAC", StringComparison.Ordinal);
        }

        static bool EstLogement(string? l)
            => !string.IsNullOrWhiteSpace(l) &&
               l.Contains("logement", StringComparison.OrdinalIgnoreCase);

        return (EstKm(libelleReference) && EstKm(libelleAutre))
               || (EstLogement(libelleReference) && EstLogement(libelleAutre));
    }

    private static int? TrouverPrimeKmId(PaieDbContext db)
        => db.PrimesIndemnites.AsNoTracking().AsEnumerable()
            .Where(p =>
            {
                var l = p.Libelle.ToUpperInvariant();
                return l.Contains("KM", StringComparison.Ordinal) ||
                       l.Contains("DEPLAC", StringComparison.Ordinal) ||
                       l.Contains("KILOM", StringComparison.Ordinal);
            })
            .OrderBy(p => p.Libelle.Contains("KM", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(p => p.Id)
            .FirstOrDefault()?.Id;

    private static int? TrouverPrimeLogementId(PaieDbContext db)
        => db.PrimesIndemnites.AsNoTracking().AsEnumerable()
            .FirstOrDefault(p => p.Libelle.Contains("logement", StringComparison.OrdinalIgnoreCase))?.Id;
}
