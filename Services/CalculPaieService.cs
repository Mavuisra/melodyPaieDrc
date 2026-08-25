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
    private const string RubHeuresSup = "HEURES_SUP";
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
    private const string RubTransportAbsences = "TRANSPORT_ABSENCES";

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
    /// - Heures supplémentaires (majoration contractuelle sur heures au-delà du nominal)
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

        // Quinzaines → acomptes : toujours synchroniser avant le calcul pour déduire les dettes à la paie.
        QuinzaineOctroiService.SynchroniserAcomptesPeriode(_db, employeId, periodePaieId);
        _db.SaveChanges();

        var entrepriseId = ContexteEntrepriseService.ObtenirEntrepriseIdEmploye(_db, employeId);
        var politique = _politiqueService.Charger(entrepriseId);
        var joursReferencePaie = politique.JoursReferencePaie;
        var heuresParJour = politique.HeuresParJour;
        var salaireBaseDejaNet = politique.SalaireContratEnNet;

        // Recherche du contrat actif sur la période
        var (dateDebutPeriode, dateFinPeriode) = PeriodePaieHelper.ObtenirBornes(periode, politique);
        var aujourdHui = DateTime.Today;
        var dateFinCalcul = PeriodePaieHelper.ObtenirFinCalcul(periode, politique, aujourdHui);

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

        var calendrierCtx = SuiviJournalierCalculPaieHelper.ChargerCalendrierPaie(_db, dateDebutPeriode, dateFinCalcul);
        var calendrierPaie = calendrierCtx.Calendrier;
        var semaineSixJoursPaie = calendrierCtx.SemaineSixJours || politique.ForcerSamediOuvre;
        var reglesLt = LtServicesReglesProvider.ChargerDepuisDb(_db);

        decimal joursPointesDepuisSuivi = 0m;
        List<SuiviJournalier> suivisComptables = new();
        var heuresLtCumulPeriode = 0m;
        var heuresSupCumulPeriode = 0m;
        if (suivisJournaliers.Count > 0 || politique.CompleterJoursSansSaisie || politique.ForcerSamediOuvre)
        {
            var suivisCompletsPourPaie = SuiviJournalierGrilleHelper.FusionnerMoisCompletPourCalculPaie(
                employeId,
                dateDebutPeriode,
                dateFinCalcul,
                suivisJournaliers,
                semaineSixJoursPaie,
                calendrierPaie,
                politique.CompleterJoursSansSaisie,
                politique.ForcerSamediOuvre);

            suivisComptables = suivisCompletsPourPaie
                .Where(s => string.Equals(s.TypeJour, SuiviJournalier.TypeNormal, StringComparison.OrdinalIgnoreCase))
                .Where(s => SuiviJournalierCalculPaieHelper.RecalculerHeuresEffectives(s, reglesLt) > 0m)
                .ToList();

            foreach (var sj in suivisComptables)
            {
                heuresLtCumulPeriode += SuiviJournalierCalculPaieHelper.RecalculerHeuresEffectives(sj, reglesLt);
                var heuresNominales = SuiviJournalierCalculPaieHelper.DeterminerHeuresNominalesJour(
                    sj.Date, semaineSixJoursPaie, calendrierPaie, reglesLt);
                heuresSupCumulPeriode += SuiviJournalierCalculPaieHelper.CalculerHeuresSupplementairesJour(
                    sj, heuresNominales, reglesLt);
            }

            var joursEquiv = SuiviJournalierCalculPaieHelper.CalculerJoursEquivalentsPaie(
                suivisComptables, semaineSixJoursPaie, calendrierPaie, reglesLt);
            joursPointesDepuisSuivi = RoundPaie(joursEquiv);
        }

        // Jours payés à 100% sans indemnités : maladie, congé de circonstance, congé annuel.
        var joursSpeciauxPayes = suivisJournaliers
            .Where(s => s.Date >= dateDebutPeriode && s.Date <= dateFinCalcul)
            .Where(s => SuiviJournalier.EstTypeJourSpecialPaye(s.TypeJour))
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
            salaireBrut = RoundPaie(salaireBrutComplet * joursPayesSalaire / joursReferencePaie);
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

        // En mode « salaire contrat en net », salaireBrut reste le NET cible (proratisé).
        // La reconstitution brut se fait APRÈS l'ajout des primes/HS pour ne pas casser le net.

        var tauxHoraireContratRef = contrat.SalaireBase > 0 && heuresParJour > 0
            ? RoundPaie(contrat.SalaireBase / joursReferencePaie / heuresParJour)
            : 0m;

        // Gains : salaire + primes / indemnités (montants mensuels selon ModeCalcul).
        decimal totalGainImposable = salaireBrut;
        decimal totalGainNonImposable = 0m;
        decimal baseCotisable = salaireBrut;

        var affectationsPrimes = _db.AffectationsPrimesIndemnites
            .Where(a => a.EmployeId == employeId)
            .Select(a => new { a.Montant, a.PrimeIndemniteId })
            .ToList();
        var primeIds = affectationsPrimes.Select(a => a.PrimeIndemniteId).Distinct().ToList();
        var primes = _db.PrimesIndemnites
            .Where(p => primeIds.Contains(p.Id) && (p.EntrepriseId == null || p.EntrepriseId == entrepriseId))
            .ToDictionary(p => p.Id);
        var detailsPrimesGains = new List<(string Libelle, decimal BaseAffichee, decimal TauxEffectif, decimal Montant)>();
        var detailsPrimesRetenues = new List<(string Libelle, decimal Montant)>();
        decimal retenuesPrimes = 0m;
        foreach (var aff in affectationsPrimes)
        {
            if (!primes.TryGetValue(aff.PrimeIndemniteId, out var prime)) continue;
            var montantMensuel = RoundPaie(aff.Montant);
            var (montant, baseAffichee, tauxEffectif) = PrimeIndemniteCalculHelper.CalculerMontant(
                montantMensuel,
                prime.ModeCalcul,
                joursPointesDepuisSuivi,
                joursReferencePaie,
                joursPrestesEffectifs);
            if (string.Equals(prime.TypeLigne, PrimeIndemnite.TypeRetenue, StringComparison.OrdinalIgnoreCase))
            {
                detailsPrimesRetenues.Add((prime.Libelle, montant));
                retenuesPrimes += montant;
            }
            else
            {
                detailsPrimesGains.Add((prime.Libelle, baseAffichee, tauxEffectif, montant));
                if (prime.EstImposable)
                    totalGainImposable += montant;
                else
                    totalGainNonImposable += montant;
                if (prime.EstCotisable)
                    baseCotisable += montant;
            }
        }

        // Heures supplémentaires : majoration contractuelle sur heures au-delà du nominal.
        decimal montantHeuresSup = 0m;
        decimal tauxHoraireSupMajoré = 0m;
        if (joursPrestesEffectifs > 0m && heuresSupCumulPeriode > 0m && tauxHoraireContratRef > 0m &&
            contrat.TauxMajorationHeuresSup > 0m)
        {
            tauxHoraireSupMajoré = RoundPaie(tauxHoraireContratRef * (1m + contrat.TauxMajorationHeuresSup / 100m));
            montantHeuresSup = RoundPaie(heuresSupCumulPeriode * tauxHoraireSupMajoré);
            totalGainImposable += montantHeuresSup;
            baseCotisable += montantHeuresSup;
        }

        // Autres ajustements saisis (gains / retenues)
        if (saisie != null)
        {
            if (saisie.AutresGainsImposables != 0)
            {
                var montant = RoundPaie(saisie.AutresGainsImposables);
                totalGainImposable += montant;
                baseCotisable += montant;
            }

            if (saisie.AutresGainsNonImposables != 0)
            {
                var montant = RoundPaie(saisie.AutresGainsNonImposables);
                totalGainNonImposable += montant;
            }
        }

        // Salaire contrat en net : reconstituer le brut sur le net imposable (base + primes imposables + HS).
        // Les gains non imposables (ex. transport) restent au montant contractuel.
        if (salaireBaseDejaNet && totalGainImposable > 0 && joursPrestesEffectifs > 0m)
        {
            var netCibleImposable = totalGainImposable;
            var brutReconstitue = ReconstituerBrutDepuisNet(netCibleImposable, nbEnfants, entrepriseId);
            if (brutReconstitue > 0 && netCibleImposable > 0)
            {
                var facteur = brutReconstitue / netCibleImposable;
                salaireBrut = RoundPaie(salaireBrut * facteur);
                montantHeuresSup = RoundPaie(montantHeuresSup * facteur);
                var libellesNonImposables = primes.Values
                    .Where(p => !p.EstImposable)
                    .Select(p => p.Libelle)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                detailsPrimesGains = detailsPrimesGains
                    .Select(x => libellesNonImposables.Contains(x.Libelle)
                        ? x
                        : (x.Libelle, x.BaseAffichee, x.TauxEffectif, RoundPaie(x.Montant * facteur)))
                    .ToList();
                var primesCotisablesNonImposables = detailsPrimesGains
                    .Where(x =>
                    {
                        var p = primes.Values.FirstOrDefault(pr =>
                            string.Equals(pr.Libelle, x.Libelle, StringComparison.OrdinalIgnoreCase));
                        return p is { EstCotisable: true, EstImposable: false };
                    })
                    .Sum(x => x.Montant);
                baseCotisable = RoundPaie(brutReconstitue + primesCotisablesNonImposables);
                totalGainImposable = brutReconstitue;
            }
        }

        // Transport : gain mensuel plein, puis retenue des jours de non-présence réelle
        // (maladie/congé/absence exclus des jours pointés). Ex. 62,40/26 = 2,40 $/jour.
        decimal retenueTransportAbsences = 0m;
        decimal tauxTransportJournalier = 0m;
        decimal joursTransportNonPresents = 0m;
        var montantTransportMensuel = detailsPrimesGains
            .Where(x => TransportAbsencePaieHelper.EstIndemniteTransport(x.Libelle))
            .Sum(x => x.Montant);
        if (montantTransportMensuel > 0m)
        {
            (retenueTransportAbsences, tauxTransportJournalier, joursTransportNonPresents) =
                TransportAbsencePaieHelper.CalculerCoupe(
                    montantTransportMensuel,
                    joursPointesDepuisSuivi,
                    joursReferencePaie);
        }

        var baseImposableIpr = RoundPaie(Math.Max(0m, totalGainImposable));
        baseCotisable = RoundPaie(Math.Max(0m, baseCotisable));

        var iprDetails = politique.UtiliserBaremeIpr
            ? _iprService.CalculerDetailsIprMensuelle(baseImposableIpr, nbEnfants, entrepriseId)
            : new IprResultat();
        var iprNet = iprDetails.IprNet;
        var reductionFamille = iprDetails.ReductionFamille;

        var cotisations = politique.UtiliserTauxSociauxDb
            ? _cotisationsService.Calculer(baseCotisable, entrepriseId)
            : new CotisationsResultat();
        var cnssOuvrierMontant = cotisations.CnssOuvrier;
        var inppMontant = cotisations.Inpp;

        // Stagiaires : pas de CNSS / IPR / INPP
        var estStagiaire = string.Equals(contrat.TypeContrat, "Stage", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(contrat.TypeContrat, "Stagiaire", StringComparison.OrdinalIgnoreCase);
        if (estStagiaire)
        {
            iprNet = 0m;
            cnssOuvrierMontant = 0m;
            inppMontant = 0m;
            reductionFamille = 0m;
            iprDetails = new IprResultat
            {
                BaseImposable = iprDetails.BaseImposable,
                IprBrut = 0m,
                ReductionFamille = 0m,
                IprNet = 0m
            };
        }

        var tauxIprAffiche = baseImposableIpr > 0 ? RoundPaie(iprNet / baseImposableIpr * 100m) : 0m;
        var tauxCnssAffiche = cotisations.TauxCnssOuvrier;
        var tauxInppAffiche = cotisations.TauxInpp;

        if (joursPrestesEffectifs <= 0m)
        {
            iprNet = 0m;
            cnssOuvrierMontant = 0m;
            inppMontant = 0m;
            baseImposableIpr = 0m;
            baseCotisable = 0m;
            reductionFamille = 0m;
            tauxIprAffiche = 0m;
            tauxCnssAffiche = 0m;
            tauxInppAffiche = 0m;
        }

        // Références fiche Excel (CDF) : uniquement si le contrat est en CDF (évite de déduire des montants FC sur un salaire USD).
        var contratEnCdf = string.Equals(contrat.DeviseBase, "CDF", StringComparison.OrdinalIgnoreCase);
        var baseIprAffiche = contratEnCdf && employe.ReferenceBrutImposableCnssCdf is decimal rbf && rbf > 0
            ? RoundPaie(rbf)
            : baseImposableIpr;

        if (joursPrestesEffectifs > 0m && contratEnCdf && employe.ReferenceIprNetCdf.HasValue)
            iprNet = RoundPaie(employe.ReferenceIprNetCdf.Value);

        if (joursPrestesEffectifs > 0m && contratEnCdf && employe.ReferenceCnssOuvrierCdf.HasValue)
            cnssOuvrierMontant = RoundPaie(employe.ReferenceCnssOuvrierCdf.Value);

        if (joursPrestesEffectifs > 0m && contratEnCdf && employe.ReferenceInppCdf.HasValue)
            inppMontant = RoundPaie(employe.ReferenceInppCdf.Value);

        var basePourTauxRetenues = contratEnCdf && employe.ReferenceBrutImposableCnssCdf is decimal rbrut && rbrut > 0
            ? RoundPaie(rbrut)
            : baseCotisable;

        // Échéances de prêts / avances en cours (retenues mensuelles à partir de la date de début d'échéance)
        var periodeDebut = new DateTime(periode.Annee, periode.Mois, 1);
        var pretsEnCours = _db.PretsAvances
            .Where(p => p.EmployeId == employeId && p.SoldeRestant > 0)
            .ToList()
            .Where(p =>
            {
                var debut = (p.DateDebutEcheance ?? p.DateOctroi).Date;
                var debutMois = new DateTime(debut.Year, debut.Month, 1);
                return periodeDebut >= debutMois;
            })
            .ToList();
        var retenuePrets = pretsEnCours.Sum(p => Math.Min(p.MontantMensuel, p.SoldeRestant));

        var acomptesSaisis = saisie != null ? RoundPaie(saisie.AcomptesSalaire) : 0m;
        var sanctionsSaisies = saisie != null ? RoundPaie(saisie.SanctionsDisciplinaires) : 0m;
        var sanctionsRetardsAuto = politique.RetardSanctionActive
            ? RoundPaie(RetardPaieHelper.CalculerSanctionsPeriode(
                politique, employe, contrat, suivisJournaliers, reglesLt))
            : 0m;
        var totalSanctions = sanctionsSaisies + sanctionsRetardsAuto;
        var autresRetenuesSaisies = saisie != null ? RoundPaie(saisie.AutresRetenues) : 0m;

        // Retenues sociales/fiscales (reconstituées si salaire contrat en net) + dettes employé (prêts, quinzaines, sanctions, transport absences).
        var totalRetenuesSociales = iprNet + cnssOuvrierMontant + inppMontant;
        var totalRetenuesDettes = retenuePrets + retenuesPrimes + acomptesSaisis + totalSanctions
                                  + autresRetenuesSaisies + RoundPaie(retenueTransportAbsences);
        var totalRetenues = totalRetenuesSociales + totalRetenuesDettes;

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
            TotalGainImposable = RoundPaie(totalGainImposable),
            TotalGainNonImposable = RoundPaie(totalGainNonImposable),
            BaseIpr = RoundPaie(baseIprAffiche),
            MontantIprBrut = RoundPaie(iprDetails.IprBrut),
            ReductionFamille = RoundPaie(reductionFamille),
            MontantIprNet = RoundPaie(iprNet),
            CotisationCnssOuvrier = RoundPaie(cnssOuvrierMontant),
            CotisationInpp = RoundPaie(inppMontant),
            NetAPayer = RoundPaie(netAPayer),
            NetAPayerDeviseLocale = RoundPaie(netAPayerDeviseLocale),
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

        // Référence mensuelle → journalière / horaire, alignée fiche type « impôts & cotisation ».
        var salaireJournalierRef = contrat.SalaireBase > 0
            ? RoundPaie(contrat.SalaireBase / joursReferencePaie)
            : 0m;
        var facteurSalaireBase = salaireJournalierRef > 0
            ? RoundPaie(salaireBrut / salaireJournalierRef, 4)
            : 0m;

        AjouterDetailSiLibelle(RubSalaireBaseJour, salaireJournalierRef, facteurSalaireBase, RoundPaie(salaireBrut), 0);

        if (heuresLtCumulPeriode > 0 && tauxHoraireContratRef > 0)
            AjouterDetailSiLibelle(RubHeuresLtPeriode, RoundPaie(heuresLtCumulPeriode), tauxHoraireContratRef, 0, 0);

        if (montantHeuresSup > 0 && tauxHoraireSupMajoré > 0)
            AjouterDetailSiLibelle(RubHeuresSup, RoundPaie(heuresSupCumulPeriode), tauxHoraireSupMajoré, montantHeuresSup, 0);

        // Informatif : montant théorique non payé (déjà proratisé dans le salaire de base).
        if (retenueAbsence > 0 || nbJoursAbsentsNonPayes > 0)
        {
            AjouterDetailSiLibelle(
                aSuspension ? RubSuspension : RubAbsence,
                RoundPaie(retenueAbsence),
                nbJoursAbsentsNonPayes,
                0,
                0);
        }

        // Primes / indemnités : montant mensuel (FIXE ou prorata jours prestés).
        detailsPrimesGains = detailsPrimesGains
            .OrderBy(x =>
            {
                var prime = primes.Values.FirstOrDefault(p => string.Equals(p.Libelle, x.Libelle, StringComparison.OrdinalIgnoreCase));
                return prime?.OrdreAffichage ?? 999;
            })
            .ThenBy(x => x.Libelle, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var (libelle, baseAffichee, tauxEffectif, montant) in detailsPrimesGains)
        {
            bulletin.Details.Add(new BulletinDetail
            {
                Libelle = libelle,
                BaseCalcul = RoundPaie(baseAffichee),
                Taux = RoundPaie(tauxEffectif),
                Gain = RoundPaie(montant),
                Retenue = 0
            });
        }

        foreach (var (libelle, montant) in detailsPrimesRetenues)
        {
            bulletin.Details.Add(new BulletinDetail
            {
                Libelle = libelle,
                BaseCalcul = RoundPaie(montant),
                Taux = 0,
                Gain = 0,
                Retenue = RoundPaie(montant)
            });
        }

        var autresGainsImposables = saisie != null ? RoundPaie(saisie.AutresGainsImposables) : 0m;
        AjouterDetailSiLibelle(RubAutresGainsImposables, autresGainsImposables, 0, autresGainsImposables, 0);

        var autresGainsNonImposables = saisie != null ? RoundPaie(saisie.AutresGainsNonImposables) : 0m;
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

        AjouterDetailSiLibelle(RubPretsAvances, 0, 0, 0, RoundPaie(retenuePrets));
        AjouterDetailSiLibelle(RubAcomptes, 0, 0, 0, acomptesSaisis);
        // Saisie manuelle + sanctions auto retards (pointages) — visible sur bulletin et synthèse.
        AjouterDetailSiLibelle(RubSanctions, 0, 0, 0, totalSanctions);
        if (retenueTransportAbsences > 0)
        {
            TryLibelleRubrique(RubTransportAbsences, out var libTransport);
            bulletin.Details.Add(new BulletinDetail
            {
                Libelle = $"{libTransport} ({joursTransportNonPresents:0.##} j × {tauxTransportJournalier:0.####})",
                BaseCalcul = RoundPaie(montantTransportMensuel),
                Taux = RoundPaie(tauxTransportJournalier),
                Gain = 0,
                Retenue = RoundPaie(retenueTransportAbsences)
            });
        }
        AjouterDetailSiLibelle(RubAjustementsRetenues, 0, 0, 0, autresRetenuesSaisies);

        _db.BulletinsPaie.Add(bulletin);
        _db.SaveChanges();

        // Mise à jour des soldes des prêts / avances (une échéance déduite par bulletin)
        foreach (var p in pretsEnCours)
        {
            var preleve = Math.Min(p.MontantMensuel, p.SoldeRestant);
            p.SoldeRestant -= preleve;
            if (p.SoldeRestant < 0) p.SoldeRestant = 0;
            if (p.SoldeRestant == 0) p.Statut = "Terminé";
        }
        if (pretsEnCours.Count > 0)
            _db.SaveChanges();

        return bulletin;
    }

    private static decimal RoundPaie(decimal value, int decimals = 2)
        => decimal.Round(value, decimals, MidpointRounding.AwayFromZero);

    private decimal ReconstituerBrutDepuisNet(decimal netCible, int nbEnfants, int entrepriseId)
    {
        if (netCible <= 0) return 0m;

        decimal NetDepuisBrut(decimal brut)
        {
            var ipr = _iprService.CalculerDetailsIprMensuelle(brut, nbEnfants, entrepriseId).IprNet;
            var cot = _cotisationsService.Calculer(brut, entrepriseId);
            var net = brut - ipr - cot.CnssOuvrier - cot.Inpp;
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

        return RoundPaie(haut);
    }

    /// <summary>
    /// Génère un bulletin pour chaque employé ayant un contrat actif sur la période
    /// et n'ayant pas déjà de bulletin pour cette période.
    /// </summary>
    /// <param name="periodePaieId">Identifiant de la période de paie.</param>
    /// <returns>Nombre généré, déjà existants, éligibles (contrat actif), erreurs par employé.</returns>
    public (int Generes, int DejaGeneres, int Eligibles, List<string> Erreurs) GenererBulletinsPourTous(int periodePaieId)
    {
        var periode = _db.PeriodesPaie.FirstOrDefault(p => p.Id == periodePaieId)
                      ?? throw new InvalidOperationException("Période de paie introuvable.");

        if (periode.Cloturee)
            throw new InvalidOperationException("Cette période est clôturée. Impossible de générer des bulletins.");

        QuinzaineOctroiService.SynchroniserAcomptesPeriodePourTous(_db, periodePaieId);
        _db.SaveChanges();

        var entrepriseId = ContexteEntrepriseService.ObtenirEntrepriseCouranteId(_db);
        var politique = _politiqueService.Charger(entrepriseId);
        var (dateDebutPeriode, dateFinPeriode) = PeriodePaieHelper.ObtenirBornes(periode, politique);

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
        var dejaGeneres = employeIdsAvecContrat.Count - aTraiter.Count;
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

        return (generes, dejaGeneres, employeIdsAvecContrat.Count, erreurs);
    }

    private Dictionary<string, string> ChargerLibellesEmploye(int employeId)
    {
        return _db.EmployesLibellesBulletin
            .Where(x => x.EmployeId == employeId)
            .ToDictionary(x => x.CodeRubrique, x => x.Libelle, StringComparer.OrdinalIgnoreCase);
    }

}

