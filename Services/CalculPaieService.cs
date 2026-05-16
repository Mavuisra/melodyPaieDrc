using System.Collections.Generic;
using System.Linq;
using MelodyPaieRDC.Data;
using MelodyPaieRDC.Helpers;
using MelodyPaieRDC.Models;

namespace MelodyPaieRDC.Services;

/// <summary>
/// Service central de calcul de paie pour une période donnée.
/// Combine salaire de base, IPR, cotisations sociales et avances pour produire un BulletinPaie.
/// </summary>
public class CalculPaieService
{
    private const string RubSalaireBaseJour = "SALAIRE_BASE_JOUR";
    private const string RubHeuresLtPeriode = "HEURES_LT_PERIODE";
    private const string RubAbsenceInfo = "ABSENCE_INFO";
    private const string RubAbsence = "ABSENCE_NON_REMUNEREE";
    private const string RubSuspension = "SUSPENSION_CONTRAT";
    private const string RubAutresGainsImposables = "AUTRES_GAINS_IMPOSABLES";
    private const string RubAutresGainsNonImposables = "AUTRES_GAINS_NON_IMPOSABLES";
    private const string RubIpr = "IPR";
    private const string RubCnss = "CNSS";
    private const string RubInpp = "INPP";
    private const string RubPretsAvances = "PRETS_AVANCES";
    private const string RubAcomptes = "ACOMPTES_SALAIRE";
    private const string RubSanctions = "SANCTIONS_DISCIPLINAIRES";
    private const string RubAjustementsRetenues = "AJUSTEMENTS_RETENUES";

    private readonly PaieDbContext _db;
    private readonly CalculeIPRService _iprService;
    private readonly CotisationsSocialesService _cotisationsService;
    private readonly PolitiquePaieService _politiqueService;

    public CalculPaieService(PaieDbContext db)
    {
        _db = db;
        _iprService = new CalculeIPRService(db);
        _cotisationsService = new CotisationsSocialesService(db);
        _politiqueService = new PolitiquePaieService(db);
    }

    /// <summary>
    /// Génère et enregistre un bulletin de paie pour un employé et une période.
    /// Pour l'instant, le calcul se base sur :
    /// - Salaire de base du contrat actif
    /// - Présence : suivi journalier (heures recalculées depuis les pointages LT si calcul auto, puis jours équivalents pondérés 8 h / 5 h selon le calendrier), sinon saisie paie, sinon absences
    /// - IPR (barème + plafond + réduction famille)
    /// - CNSS part ouvrière
    /// - Échéances de prêts / avances (MontantMensuel, si solde > 0)
    /// Les autres gains (primes, heures sup, etc.) pourront être ajoutés plus tard.
    /// </summary>
    public BulletinPaie GenererBulletin(int employeId, int periodePaieId)
    {
        var employe = _db.Employes.FirstOrDefault(e => e.Id == employeId)
                      ?? throw new InvalidOperationException("Employé introuvable.");

        var periode = _db.PeriodesPaie.FirstOrDefault(p => p.Id == periodePaieId)
                      ?? throw new InvalidOperationException("Période de paie introuvable.");

        if (periode.Cloturee)
            throw new InvalidOperationException("Cette période est clôturée. Impossible de générer un nouveau bulletin.");

        var tauxCdfUsd = ParametresApplicationHelper.GetTauxCdfParUsd(_db);
        if (tauxCdfUsd <= 0)
            tauxCdfUsd = periode.TauxChangeBudget;

        if (_db.BulletinsPaie.Any(b => b.EmployeId == employeId && b.PeriodePaieId == periodePaieId))
            throw new InvalidOperationException("Un bulletin existe déjà pour cet employé et cette période.");

        var entrepriseId = ContexteEntrepriseService.ObtenirEntrepriseIdEmploye(_db, employeId);
        var politique = _politiqueService.Charger(entrepriseId);
        var joursReferencePaie = politique.JoursReferencePaie;
        var heuresParJour = politique.HeuresParJour;
        var salaireBaseDejaNet = politique.SalaireContratEnNet;

        // Recherche du contrat actif sur la période (simplifié : contrat sans date fin ou fin >= début de période)
        var dateDebutPeriode = new DateTime(periode.Annee, periode.Mois, 1);
        var dateFinPeriode = dateDebutPeriode.AddMonths(1).AddDays(-1);
        // Si la période en cours n'est pas clôturée, on évite de payer les jours futurs du mois.
        var aujourdHui = DateTime.Today;
        var periodeEnCoursNonCloturee = periode.Annee == aujourdHui.Year && periode.Mois == aujourdHui.Month && !periode.Cloturee;
        var dateFinCalcul = periodeEnCoursNonCloturee && aujourdHui < dateFinPeriode
            ? aujourdHui
            : dateFinPeriode;

        var contrat = _db.Contrats
            .Where(c => c.EmployeId == employeId &&
                        c.DateDebut <= dateFinPeriode &&
                        (c.DateFin == null || c.DateFin >= dateDebutPeriode))
            .OrderByDescending(c => c.DateDebut)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Aucun contrat actif trouvé pour cette période.");

        // Salaire brut de référence = salaire de base du contrat
        var salaireBrutComplet = contrat.SalaireBase;

        var joursDansPeriode = (int)joursReferencePaie;

        // Saisie manuelle éventuelle pour cette période / cet employé
        var saisie = _db.SaisiesPaie.FirstOrDefault(s => s.EmployeId == employeId && s.PeriodePaieId == periodePaieId);

        // Suivi journalier : aligné sur les données réelles (pointages LT, calendrier ouvré / samedi)
        var suivisJournaliers = _db.SuivisJournaliers
            .Where(s => s.EmployeId == employeId && s.Date >= dateDebutPeriode && s.Date <= dateFinCalcul)
            .ToList();

        var calendrierPaie = _db.JoursTravailCalendrier
            .Where(j => j.Annee == periode.Annee && j.DateJour >= dateDebutPeriode && j.DateJour <= dateFinCalcul)
            .ToDictionary(j => j.DateJour.Date);

        var semaineSixJoursPaie = calendrierPaie.Any(kvp =>
            kvp.Key.DayOfWeek == DayOfWeek.Saturday &&
            string.Equals(kvp.Value.TypeJour, "Ouvre", StringComparison.OrdinalIgnoreCase));
        var reglesLt = LtServicesReglesProvider.ChargerDepuisDb(_db);

        decimal joursPointesDepuisSuivi = 0m;
        List<SuiviJournalier>? suivisCompletsPourPaie = null;
        var heuresLtCumulPeriode = 0m;
        if (suivisJournaliers.Count > 0)
        {
            // Mois complet comme sur la grille : jours sans ligne en base complétés (mois partiellement renseigné, etc.).
            suivisCompletsPourPaie = SuiviJournalierGrilleHelper.FusionnerMoisCompletPourCalculPaie(
                employeId,
                dateDebutPeriode,
                dateFinCalcul,
                suivisJournaliers,
                semaineSixJoursPaie,
                calendrierPaie);

            // Mode strict demandé : seuls les pointages terminal (PointagesJson) comptent pour la paie.
            var suivisAvecPointages = suivisCompletsPourPaie
                .Where(s => !string.IsNullOrWhiteSpace(s.PointagesJson))
                .ToList();

            foreach (var sj in suivisAvecPointages)
                heuresLtCumulPeriode += PointagesJournalierSerializer.CalculerHeuresLt(sj.PointagesJson, sj.Date, reglesLt);

            var joursEquiv = SuiviJournalierCalculPaieHelper.CalculerJoursEquivalentsPaie(
                suivisAvecPointages, semaineSixJoursPaie, calendrierPaie, reglesLt);
            joursPointesDepuisSuivi = decimal.Round(joursEquiv, 2);
        }

        // Jours payés à 100% sans indemnités : maladie + congé de circonstance.
        var joursSpeciauxPayes = suivisJournaliers
            .Where(s => s.Date >= dateDebutPeriode && s.Date <= dateFinCalcul)
            .Where(s => string.Equals(s.TypeJour, SuiviJournalier.TypeMaladie, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(s.TypeJour, SuiviJournalier.TypeCongeCirconstance, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Date.Date)
            .Distinct()
            .Count();

        // Jours d'absence non rémunérée dans la période (distincts)
        var joursAbsenceNonPayes = new HashSet<DateTime>();
        var absencesNonPayees = _db.AbsencesConges
            .Where(a => a.EmployeId == employeId && !a.EstPaye &&
                        a.DateDebut <= dateFinCalcul && a.DateFin >= dateDebutPeriode)
            .ToList();
        foreach (var a in absencesNonPayees)
        {
            var debut = a.DateDebut < dateDebutPeriode ? dateDebutPeriode : a.DateDebut;
            var fin = a.DateFin > dateFinCalcul ? dateFinCalcul : a.DateFin;
            for (var d = debut; d <= fin; d = d.AddDays(1))
                joursAbsenceNonPayes.Add(d);
        }
        foreach (var s in suivisJournaliers.Where(s =>
                     string.Equals(s.TypeJour, SuiviJournalier.TypeAbsence, StringComparison.OrdinalIgnoreCase)))
        {
            joursAbsenceNonPayes.Add(s.Date.Date);
        }
        var nbJoursAbsentsNonPayes = joursAbsenceNonPayes.Count;
        if (nbJoursAbsentsNonPayes > joursDansPeriode) nbJoursAbsentsNonPayes = joursDansPeriode;
        var aSuspension = absencesNonPayees.Any(a =>
            a.Type.Contains("suspension", StringComparison.OrdinalIgnoreCase));

        // Salaire brut proportionnel : pointages réels + jours spéciaux payés (maladie/congé).
        decimal salaireBrut;
        decimal joursPrestesEffectifs;
        if (string.Equals(politique.ModeCalculPresence, ParametrePolitiquePaie.ModePresenceSaisieJours, StringComparison.OrdinalIgnoreCase)
            && saisie != null && saisie.JoursPrestes > 0)
        {
            joursPointesDepuisSuivi = saisie.JoursPrestes;
        }
        else if (string.Equals(politique.ModeCalculPresence, ParametrePolitiquePaie.ModePresenceHybride, StringComparison.OrdinalIgnoreCase)
                 && saisie != null && saisie.JoursPrestes > 0 && joursPointesDepuisSuivi <= 0)
        {
            joursPointesDepuisSuivi = saisie.JoursPrestes;
        }

        var joursPayesSalaire = Math.Min(joursReferencePaie, Math.Max(0m, joursPointesDepuisSuivi) + joursSpeciauxPayes);
        if (joursPayesSalaire > 0)
        {
            joursPrestesEffectifs = joursPayesSalaire;
            salaireBrut = decimal.Round(salaireBrutComplet * joursPayesSalaire / joursReferencePaie, 2);
            var joursArrondis = (int)Math.Round((double)joursPayesSalaire, MidpointRounding.AwayFromZero);
            nbJoursAbsentsNonPayes = Math.Max(0, joursDansPeriode - joursArrondis);
        }
        else
        {
            // Sans pointage terminal, considéré non travaillé.
            joursPrestesEffectifs = 0m;
            salaireBrut = 0m;
        }
        var retenueAbsence = salaireBrutComplet - salaireBrut;

        // Nombre d'enfants à charge (AyantDroit avec LienParente = "Enfant")
        var nbEnfants = _db.AyantsDroit
            .Count(a => a.EmployeId == employeId && a.LienParente.ToLower().Contains("enfant"));

        // Chez LTS, le contrat contient un net cible historique.
        // On reconstruit le brut (net -> brut) pour pouvoir afficher IPR/CNSS
        // tout en conservant le net contractuel comme cible.
        if (salaireBaseDejaNet && salaireBrut > 0)
            salaireBrut = ReconstituerBrutDepuisNet(salaireBrut, nbEnfants, entrepriseId);

        // Gains : salaire + primes / indemnités.
        // Règle métier : montants des affectations saisis au module employé = journalier.
        decimal totalGainImposable = salaireBrut;
        decimal totalGainNonImposable = 0m;

        var affectationsPrimes = _db.AffectationsPrimesIndemnites
            .Where(a => a.EmployeId == employeId)
            .Select(a => new { a.Montant, a.PrimeIndemniteId })
            .ToList();
        var primeIds = affectationsPrimes.Select(a => a.PrimeIndemniteId).Distinct().ToList();
        var primes = _db.PrimesIndemnites
            .Where(p => primeIds.Contains(p.Id) && (p.EntrepriseId == null || p.EntrepriseId == entrepriseId))
            .ToDictionary(p => p.Id);
        var detailsPrimesGains = new List<(string Libelle, decimal BaseJour, decimal TauxEffectif, decimal Montant)>();
        var detailsPrimesRetenues = new List<(string Libelle, decimal Montant)>();
        decimal retenuesPrimes = 0m;
        foreach (var aff in affectationsPrimes)
        {
            if (!primes.TryGetValue(aff.PrimeIndemniteId, out var prime)) continue;
            var montantJournalier = decimal.Round(aff.Montant, 2);
            var montant = decimal.Round(montantJournalier * joursPointesDepuisSuivi, 2);
            if (string.Equals(prime.TypeLigne, PrimeIndemnite.TypeRetenue, StringComparison.OrdinalIgnoreCase))
            {
                detailsPrimesRetenues.Add((prime.Libelle, montant));
                retenuesPrimes += montant;
            }
            else
            {
                // Indemnités uniquement sur jours réellement pointés (pas sur maladie/congé payés).
                detailsPrimesGains.Add((prime.Libelle, montantJournalier, joursPointesDepuisSuivi, montant));
                if (prime.EstImposable)
                    totalGainImposable += montant;
                
                else
                    totalGainNonImposable += montant;
            }
        }

        // Autres ajustements saisis (gains / retenues)
        if (saisie != null)
        {
            if (saisie.AutresGainsImposables != 0)
            {
                var montant = decimal.Round(saisie.AutresGainsImposables, 2);
                totalGainImposable += montant;
            }

            if (saisie.AutresGainsNonImposables != 0)
            {
                var montant = decimal.Round(saisie.AutresGainsNonImposables, 2);
                totalGainNonImposable += montant;
            }

            if (saisie.AutresRetenues != 0)
            {
                // Autres retenues ajoutées plus loin dans les détails, ici on ajuste seulement le net.
            }
        }

        // Base légale mensuelle de retenues : salaire du mois (base + indemnités de gains)
        var baseLegaleRetenues = decimal.Round(Math.Max(0m, totalGainImposable + totalGainNonImposable), 2);
        var baseImposable = baseLegaleRetenues;

        var iprDetails = politique.UtiliserBaremeIpr
            ? _iprService.CalculerDetailsIprMensuelle(baseLegaleRetenues, nbEnfants, entrepriseId)
            : new IprResultat();
        var iprNet = iprDetails.IprNet;
        var reductionFamille = iprDetails.ReductionFamille;

        var cotisations = politique.UtiliserTauxSociauxDb
            ? _cotisationsService.Calculer(baseLegaleRetenues, entrepriseId)
            : new CotisationsResultat();
        var cnssOuvrierMontant = cotisations.CnssOuvrier;
        var inppMontant = cotisations.Inpp;
        var tauxIprAffiche = baseLegaleRetenues > 0 ? decimal.Round(iprNet / baseLegaleRetenues * 100m, 2) : 0m;
        var tauxCnssAffiche = cotisations.TauxCnssOuvrier;
        var tauxInppAffiche = cotisations.TauxInpp;

        if (joursPrestesEffectifs <= 0m)
        {
            iprNet = 0m;
            cnssOuvrierMontant = 0m;
            inppMontant = 0m;
            baseImposable = 0m;
            reductionFamille = 0m;
            tauxIprAffiche = 0m;
            tauxCnssAffiche = 0m;
            tauxInppAffiche = 0m;
        }

        // Références fiche Excel (CDF) sur l’employé : mêmes montants que la grille importée
        var baseIprAffiche = employe.ReferenceBrutImposableCnssCdf is decimal rbf && rbf > 0
            ? decimal.Round(rbf, 2)
            : baseLegaleRetenues;

        if (joursPrestesEffectifs > 0m && employe.ReferenceIprNetCdf.HasValue)
            iprNet = decimal.Round(employe.ReferenceIprNetCdf.Value, 2);

        if (joursPrestesEffectifs > 0m && employe.ReferenceCnssOuvrierCdf.HasValue)
            cnssOuvrierMontant = decimal.Round(employe.ReferenceCnssOuvrierCdf.Value, 2);

        if (joursPrestesEffectifs > 0m && employe.ReferenceInppCdf.HasValue)
            inppMontant = decimal.Round(employe.ReferenceInppCdf.Value, 2);

        var basePourTauxRetenues = employe.ReferenceBrutImposableCnssCdf is decimal rbrut && rbrut > 0
            ? decimal.Round(rbrut, 2)
            : baseLegaleRetenues;

        // Échéances de prêts / avances en cours (retenues mensuelles)
        // SQLite ne supporte pas Sum(decimal) en SQL : on agrège en mémoire
        var pretsEnCours = _db.PretsAvances
            .Where(p => p.EmployeId == employeId && p.SoldeRestant > 0)
            .ToList();
        var retenuePrets = pretsEnCours.Sum(p => p.MontantMensuel);

        // Retenues totales sur salaire (côté employé)
        var acomptesSaisis = saisie != null ? decimal.Round(saisie.AcomptesSalaire, 2) : 0m;
        var sanctionsSaisies = saisie != null ? decimal.Round(saisie.SanctionsDisciplinaires, 2) : 0m;
        var autresRetenuesSaisies = saisie != null ? decimal.Round(saisie.AutresRetenues, 2) : 0m;
        var totalRetenues = iprNet + cnssOuvrierMontant + inppMontant + retenuePrets + retenuesPrimes +
                            acomptesSaisis + sanctionsSaisies + autresRetenuesSaisies;

        var totalGains = totalGainImposable + totalGainNonImposable;
        var netAPayer = totalGains - totalRetenues;
        if (netAPayer < 0) netAPayer = 0;

        // Devise locale : si le contrat est déjà en CDF, pas de conversion USD→CDF sur le net.
        var netAPayerDeviseLocale = string.Equals(contrat.DeviseBase, "USD", StringComparison.OrdinalIgnoreCase) &&
                                    tauxCdfUsd > 0
            ? TauxChangeHelper.UsdVersCdf(netAPayer, tauxCdfUsd)
            : netAPayer;

        // Numéro unique du bulletin pour la période (ex. 2025-03-001)
        var nbBulletinsPeriode = _db.BulletinsPaie.Count(b => b.PeriodePaieId == periodePaieId);
        var numeroBulletin = $"{periode.Annee}-{periode.Mois:D2}-{(nbBulletinsPeriode + 1):D3}";

        var libellesEmploye = ChargerLibellesEmploye(employeId);
        bool TryLibelleRubrique(string code, out string libelle)
        {
            if (libellesEmploye.TryGetValue(code, out var lib) && !string.IsNullOrWhiteSpace(lib))
            {
                libelle = lib;
                return true;
            }

            var politiqueLib = politique.LibelleRubrique(code);
            if (!string.IsNullOrWhiteSpace(politiqueLib))
            {
                libelle = politiqueLib;
                return true;
            }

            libelle = code;
            return true;
        }

        // Création du bulletin
        var bulletin = new BulletinPaie
        {
            EmployeId = employeId,
            PeriodePaieId = periodePaieId,
            NumeroBulletin = numeroBulletin,
            DateGeneration = DateTime.Now,
            TotalGainImposable = decimal.Round(totalGainImposable, 2),
            TotalGainNonImposable = decimal.Round(totalGainNonImposable, 2),
            BaseIpr = decimal.Round(baseIprAffiche, 2),
            MontantIprBrut = decimal.Round(iprDetails.IprBrut, 2),
            ReductionFamille = decimal.Round(reductionFamille, 2),
            MontantIprNet = decimal.Round(iprNet, 2),
            CotisationCnssOuvrier = decimal.Round(cnssOuvrierMontant, 2),
            CotisationInpp = decimal.Round(inppMontant, 2),
            NetAPayer = decimal.Round(netAPayer, 2),
            NetAPayerDeviseLocale = decimal.Round(netAPayerDeviseLocale, 2),
            Details = new List<BulletinDetail>()
        };
        void AjouterDetailSiLibelle(string code, decimal baseCalcul, decimal taux, decimal gain, decimal retenue)
        {
            if (!TryLibelleRubrique(code, out var libelle))
                return;

            bulletin.Details.Add(new BulletinDetail
            {
                Libelle = libelle,
                BaseCalcul = baseCalcul,
                Taux = taux,
                Gain = gain,
                Retenue = retenue
            });
        }

        // Référence mensuelle → journalière / horaire (26 j × 8 h), alignée fiche type « impôts & cotisation ».
        var salaireJournalierRef = contrat.SalaireBase > 0
            ? decimal.Round(contrat.SalaireBase / joursReferencePaie, 2)
            : 0m;
        var tauxHoraireContratRef = contrat.SalaireBase > 0 && heuresParJour > 0
            ? decimal.Round(contrat.SalaireBase / joursReferencePaie / heuresParJour, 2)
            : 0m;
        var facteurSalaireBase = salaireJournalierRef > 0
            ? decimal.Round(salaireBrut / salaireJournalierRef, 4)
            : 0m;

        // Salaire contractuel proratisé : Base = montant / jour de réf., Taux = équivalent « nombre de jours » facturé.
        AjouterDetailSiLibelle(RubSalaireBaseJour, salaireJournalierRef, facteurSalaireBase, decimal.Round(salaireBrut, 2), 0);

        if (heuresLtCumulPeriode > 0 && tauxHoraireContratRef > 0)
        {
            AjouterDetailSiLibelle(RubHeuresLtPeriode, decimal.Round(heuresLtCumulPeriode, 2), tauxHoraireContratRef, 0, 0);
        }

        // Détail : Absence non rémunérée ou suspension de contrat (traçabilité)
        if (retenueAbsence > 0)
        {
            AjouterDetailSiLibelle(
                aSuspension ? RubSuspension : RubAbsence,
                salaireBrutComplet,
                0,
                0,
                decimal.Round(retenueAbsence, 2));
        }

        // Rubrique informative demandée : absences constatées, sans impact montant.
        if (nbJoursAbsentsNonPayes > 0)
        {
            AjouterDetailSiLibelle(
                RubAbsenceInfo,
                nbJoursAbsentsNonPayes,
                0m,
                0m,
                0m);
        }

        // Primes / indemnités : base journalière × taux réel (jours effectivement prestés).
        detailsPrimesGains = detailsPrimesGains
            .OrderBy(x =>
            {
                var prime = primes.Values.FirstOrDefault(p => string.Equals(p.Libelle, x.Libelle, StringComparison.OrdinalIgnoreCase));
                return prime?.OrdreAffichage ?? 999;
            })
            .ThenBy(x => x.Libelle, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var (libelle, baseJour, tauxEffectif, montant) in detailsPrimesGains)
        {
            var montantArrondi = decimal.Round(montant, 2);
            bulletin.Details.Add(new BulletinDetail
            {
                Libelle = libelle,
                BaseCalcul = decimal.Round(baseJour, 2),
                Taux = decimal.Round(tauxEffectif, 2),
                Gain = montantArrondi,
                Retenue = 0
            });
        }

        foreach (var (libelle, montant) in detailsPrimesRetenues)
        {
            bulletin.Details.Add(new BulletinDetail
            {
                Libelle = libelle,
                BaseCalcul = decimal.Round(montant, 2),
                Taux = 0,
                Gain = 0,
                Retenue = decimal.Round(montant, 2)
            });
        }

        // Détails : autres gains saisis (toujours affichés pour un bulletin complet)
        var autresGainsImposables = saisie != null ? decimal.Round(saisie.AutresGainsImposables, 2) : 0m;
        AjouterDetailSiLibelle(RubAutresGainsImposables, autresGainsImposables, 0, autresGainsImposables, 0);

        var autresGainsNonImposables = saisie != null ? decimal.Round(saisie.AutresGainsNonImposables, 2) : 0m;
        AjouterDetailSiLibelle(RubAutresGainsNonImposables, autresGainsNonImposables, 0, autresGainsNonImposables, 0);

        // Détails : retenues (même libellés / ordre que fiche impôts & cotisation type LTS)
        AjouterDetailSiLibelle(
            RubIpr,
            baseIprAffiche,
            tauxIprAffiche,
            0,
            iprNet);

        AjouterDetailSiLibelle(
            RubCnss,
            basePourTauxRetenues,
            tauxCnssAffiche,
            0,
            cnssOuvrierMontant);

        AjouterDetailSiLibelle(
            RubInpp,
            basePourTauxRetenues,
            tauxInppAffiche,
            0,
            inppMontant);

        AjouterDetailSiLibelle(RubPretsAvances, 0, 0, 0, decimal.Round(retenuePrets, 2));
        AjouterDetailSiLibelle(RubAcomptes, 0, 0, 0, acomptesSaisis);
        AjouterDetailSiLibelle(RubSanctions, 0, 0, 0, sanctionsSaisies);
        AjouterDetailSiLibelle(RubAjustementsRetenues, 0, 0, 0, autresRetenuesSaisies);

        _db.BulletinsPaie.Add(bulletin);
        _db.SaveChanges();

        // Mise à jour des soldes des prêts / avances (une échéance déduite par bulletin)
        foreach (var p in pretsEnCours)
        {
            p.SoldeRestant -= p.MontantMensuel;
            if (p.SoldeRestant < 0) p.SoldeRestant = 0;
            if (p.SoldeRestant == 0) p.Statut = "Terminé";
        }
        if (pretsEnCours.Count > 0)
            _db.SaveChanges();

        return bulletin;
    }

    private decimal ReconstituerBrutDepuisNet(decimal netCible, int nbEnfants, int entrepriseId)
    {
        if (netCible <= 0) return 0m;

        decimal NetDepuisBrut(decimal brut)
        {
            var ipr = _iprService.CalculerDetailsIprMensuelle(brut, nbEnfants, entrepriseId).IprNet;
            var cnss = _cotisationsService.Calculer(brut, entrepriseId).CnssOuvrier;
            var net = brut - ipr - cnss;
            return net < 0 ? 0 : net;
        }

        var bas = netCible;
        var haut = netCible * 2m;
        while (NetDepuisBrut(haut) < netCible)
            haut *= 1.2m;

        for (var i = 0; i < 50; i++)
        {
            var milieu = (bas + haut) / 2m;
            var netMilieu = NetDepuisBrut(milieu);
            if (netMilieu < netCible)
                bas = milieu;
            else
                haut = milieu;
        }

        return decimal.Round(haut, 2);
    }

    /// <summary>
    /// Génère un bulletin pour chaque employé ayant un contrat actif sur la période
    /// et n'ayant pas déjà de bulletin pour cette période.
    /// </summary>
    /// <param name="periodePaieId">Identifiant de la période de paie.</param>
    /// <returns>Nombre de bulletins générés et liste des erreurs (employé : message).</returns>
    public (int Generes, List<string> Erreurs) GenererBulletinsPourTous(int periodePaieId)
    {
        var periode = _db.PeriodesPaie.FirstOrDefault(p => p.Id == periodePaieId)
                      ?? throw new InvalidOperationException("Période de paie introuvable.");

        if (periode.Cloturee)
            throw new InvalidOperationException("Cette période est clôturée. Impossible de générer des bulletins.");

        var dateDebutPeriode = new DateTime(periode.Annee, periode.Mois, 1);
        var dateFinPeriode = dateDebutPeriode.AddMonths(1).AddDays(-1);

        // Employés ayant un contrat actif sur la période
        var employeIdsAvecContrat = _db.Contrats
            .Where(c => c.DateDebut <= dateFinPeriode && (c.DateFin == null || c.DateFin >= dateDebutPeriode))
            .Select(c => c.EmployeId)
            .Distinct()
            .ToList();

        // Exclure ceux qui ont déjà un bulletin pour cette période
        var dejaBulletin = _db.BulletinsPaie
            .Where(b => b.PeriodePaieId == periodePaieId)
            .Select(b => b.EmployeId)
            .ToHashSet();

        var aTraiter = employeIdsAvecContrat.Where(id => !dejaBulletin.Contains(id)).ToList();
        var generes = 0;
        var erreurs = new List<string>();

        foreach (var employeId in aTraiter)
        {
            try
            {
                GenererBulletin(employeId, periodePaieId);
                generes++;
            }
            catch (Exception ex)
            {
                var emp = _db.Employes.Find(employeId);
                var nom = emp != null ? $"{emp.Nom} {emp.Prenom}".Trim() : employeId.ToString();
                erreurs.Add($"{nom} : {ex.Message}");
            }
        }

        return (generes, erreurs);
    }

    private Dictionary<string, string> ChargerLibellesEmploye(int employeId)
    {
        return _db.EmployesLibellesBulletin
            .Where(x => x.EmployeId == employeId)
            .ToDictionary(x => x.CodeRubrique, x => x.Libelle, StringComparer.OrdinalIgnoreCase);
    }

}

