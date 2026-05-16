using MelodyPaieRDC.Data;
using MelodyPaieRDC.Models;
using MelodyPaieRDC.Services.Export;
using Microsoft.EntityFrameworkCore;

namespace MelodyPaieRDC.Services;

public sealed class ResultatControleCloture
{
    public string Code { get; init; } = "";
    public string Libelle { get; init; } = "";
    public string Message { get; init; } = "";
    public string Severite { get; init; } = "Erreur";
    public bool EstErreur => string.Equals(Severite, "Erreur", StringComparison.OrdinalIgnoreCase);
}

public sealed class RapportCloturePaie
{
    public int PeriodePaieId { get; init; }
    public bool PeriodeDejaCloturee { get; init; }
    public IReadOnlyList<ResultatControleCloture> Controles { get; init; } = Array.Empty<ResultatControleCloture>();
    public bool ADesErreurs => Controles.Any(c => c.EstErreur);
    public bool PeutCloturerSansForcer { get; init; }
}

public sealed class PeriodeClotureService
{
    private readonly PaieDbContext _db;

    public PeriodeClotureService(PaieDbContext db) => _db = db;

    public RapportCloturePaie Analyser(int periodePaieId)
    {
        var cfg = ConfigurationExportsPaieService.Obtenir(_db).Cloture;
        var periode = _db.PeriodesPaie
            .Include(p => p.Bulletins)
            .FirstOrDefault(p => p.Id == periodePaieId);

        if (periode is null)
            throw new InvalidOperationException("Période introuvable.");

        var resultats = new List<ResultatControleCloture>();
        var controlesActifs = cfg.Controles.Where(c => c.Actif).ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);

        if (periode.Cloturee)
        {
            resultats.Add(Creer(controlesActifs, "PERIODE_DEJA_CLOTUREE",
                "La période est déjà clôturée.", "Avertissement",
                "Cette période a déjà été clôturée."));
        }

        var lot = new PaieExportContextFactory(_db).ChargerLot(periodePaieId);
        var bulletins = lot.Lignes;

        if (EstActif(controlesActifs, "AUCUN_BULLETIN") && bulletins.Count == 0)
            resultats.Add(Creer(controlesActifs, "AUCUN_BULLETIN",
                "Au moins un bulletin doit exister pour la période.", "Erreur",
                "Aucun bulletin généré pour cette période."));

        if (EstActif(controlesActifs, "EMPLOYE_SANS_BULLETIN"))
        {
            var employesActifs = ContexteEntrepriseService.EmployesEntrepriseCourante(_db)
                .AsNoTracking()
                .Select(e => e.Id)
                .ToHashSet();
            var avecBulletin = bulletins.Select(b => b.Bulletin.EmployeId).ToHashSet();
            var manquants = employesActifs.Count(id => !avecBulletin.Contains(id));
            if (manquants > 0)
                resultats.Add(Creer(controlesActifs, "EMPLOYE_SANS_BULLETIN",
                    "Chaque employé actif doit avoir un bulletin.", "Erreur",
                    $"{manquants} employé(s) sans bulletin pour cette période."));
        }

        if (EstActif(controlesActifs, "EMPLOYE_SANS_CNSS"))
        {
            var sansCnss = bulletins.Count(c => string.IsNullOrWhiteSpace(c.Employe?.NumCnss));
            if (sansCnss > 0)
                resultats.Add(Creer(controlesActifs, "EMPLOYE_SANS_CNSS",
                    "Numéro CNSS manquant.", "Avertissement",
                    $"{sansCnss} salarié(s) sans numéro CNSS."));
        }

        if (EstActif(controlesActifs, "NET_NEGATIF"))
        {
            var negatifs = bulletins.Count(c => c.Bulletin.NetAPayer < 0);
            if (negatifs > 0)
                resultats.Add(Creer(controlesActifs, "NET_NEGATIF",
                    "Net à payer négatif interdit.", "Erreur",
                    $"{negatifs} bulletin(s) avec net négatif."));
        }

        if (EstActif(controlesActifs, "TOTAL_CNSS"))
        {
            var totalBulletins = bulletins.Sum(c => c.Bulletin.CotisationCnssOuvrier);
            var totalRecalc = bulletins.Sum(c => c.Cotisations?.CnssOuvrier ?? 0);
            if (Math.Abs(totalBulletins - totalRecalc) > 0.05m)
                resultats.Add(Creer(controlesActifs, "TOTAL_CNSS",
                    "Cohérence totaux CNSS.", "Avertissement",
                    $"Écart CNSS ouvrier : bulletins {totalBulletins:N2} vs recalcul {totalRecalc:N2}."));
        }

        if (EstActif(controlesActifs, "TOTAL_IPR"))
        {
            var total = bulletins.Sum(c => c.Bulletin.MontantIprNet);
            if (total < 0)
                resultats.Add(Creer(controlesActifs, "TOTAL_IPR",
                    "Cohérence totaux IPR.", "Avertissement", "Total IPR négatif."));
        }

        if (EstActif(controlesActifs, "COMPTE_BANQUE_MANQUANT"))
        {
            var sansCompte = bulletins.Count(c =>
                c.Bulletin.NetAPayer > 0 && string.IsNullOrWhiteSpace(c.Employe?.NumeroCompteBancaire));
            if (sansCompte > 0)
                resultats.Add(Creer(controlesActifs, "COMPTE_BANQUE_MANQUANT",
                    "Compte bancaire manquant.", "Avertissement",
                    $"{sansCompte} salarié(s) avec net > 0 sans numéro de compte."));
        }

        var erreurs = resultats.Count(r => r.EstErreur);
        var peutCloturer = cfg.ExigerControlesSansErreur ? erreurs == 0 : true;

        return new RapportCloturePaie
        {
            PeriodePaieId = periodePaieId,
            PeriodeDejaCloturee = periode.Cloturee,
            Controles = resultats,
            PeutCloturerSansForcer = peutCloturer && !periode.Cloturee
        };
    }

    public void Cloturer(int periodePaieId, string? utilisateur, bool forcerMalgreAvertissements = false)
    {
        var rapport = Analyser(periodePaieId);
        if (rapport.PeriodeDejaCloturee)
            return;

        if (!rapport.PeutCloturerSansForcer && !forcerMalgreAvertissements)
            throw new InvalidOperationException(
                "La clôture est bloquée : corrigez les erreurs signalées ou forcez en acceptant les avertissements.");

        if (!forcerMalgreAvertissements && rapport.Controles.Any(c => !c.EstErreur && c.Severite.Equals("Avertissement", StringComparison.OrdinalIgnoreCase)))
        {
            // Avertissements seuls : autorisés si pas d'erreur et ExigerControlesSansErreur respecté
        }

        var periode = _db.PeriodesPaie.Find(periodePaieId)
            ?? throw new InvalidOperationException("Période introuvable.");

        periode.Cloturee = true;
        periode.DateClotureUtc = DateTime.UtcNow;
        periode.CloturePar = string.IsNullOrWhiteSpace(utilisateur) ? "Utilisateur" : utilisateur.Trim();
        _db.SaveChanges();
    }

    public void Rouvrir(int periodePaieId)
    {
        var periode = _db.PeriodesPaie.Find(periodePaieId)
            ?? throw new InvalidOperationException("Période introuvable.");
        periode.Cloturee = false;
        periode.DateClotureUtc = null;
        periode.CloturePar = null;
        _db.SaveChanges();
    }

    public static bool PeriodeEstVerrouillee(PaieDbContext db, int periodePaieId)
    {
        return db.PeriodesPaie.AsNoTracking()
            .Where(p => p.Id == periodePaieId)
            .Select(p => p.Cloturee)
            .FirstOrDefault();
    }

    private static bool EstActif(IReadOnlyDictionary<string, ControleClotureConfig> dict, string code) =>
        dict.TryGetValue(code, out var c) && c.Actif;

    private static ResultatControleCloture Creer(
        IReadOnlyDictionary<string, ControleClotureConfig> dict,
        string code,
        string libelleDefaut,
        string severiteDefaut,
        string message)
    {
        dict.TryGetValue(code, out var cfg);
        return new ResultatControleCloture
        {
            Code = code,
            Libelle = cfg?.Libelle ?? libelleDefaut,
            Severite = cfg?.Severite ?? severiteDefaut,
            Message = message
        };
    }
}
