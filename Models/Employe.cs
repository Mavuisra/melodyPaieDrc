using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Employé : référentiel principal du personnel.
/// </summary>
public class Employe
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Matricule { get; set; } = string.Empty;

    /// <summary>ID utilisateur ZKTeco (User ID / PIN) utilisé pour la correspondance des pointages.</summary>
    [MaxLength(50)]
    public string? ZkUserId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Nom { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Postnom { get; set; }

    [MaxLength(100)]
    public string? Prenom { get; set; }

    [MaxLength(10)]
    public string? Sexe { get; set; }

    [MaxLength(50)]
    public string? EtatCivil { get; set; }

    public DateTime? DateNaissance { get; set; }

    [MaxLength(30)]
    public string? Telephone { get; set; }

    [MaxLength(255)]
    public string? Adresse { get; set; }

    /// <summary>Entreprise propriétaire (dénormalisé pour isolation multi-tenant).</summary>
    public int EntrepriseId { get; set; }

    [ForeignKey(nameof(Departement))]
    public int DepartementId { get; set; }

    public Departement? Departement { get; set; }

    public ICollection<AyantDroit> AyantsDroit { get; set; } = new List<AyantDroit>();

    public ICollection<Contrat> Contrats { get; set; } = new List<Contrat>();

    public ICollection<PretAvance> PretsAvances { get; set; } = new List<PretAvance>();

    public ICollection<AbsenceConge> AbsencesConges { get; set; } = new List<AbsenceConge>();

    public ICollection<AffectationPrimeIndemnite> AffectationsPrimesIndemnites { get; set; } = new List<AffectationPrimeIndemnite>();
    public ICollection<EmployeLibelleBulletin> LibellesBulletin { get; set; } = new List<EmployeLibelleBulletin>();

    public ICollection<BulletinPaie> BulletinsPaie { get; set; } = new List<BulletinPaie>();

    public ICollection<SuiviJournalier> SuivisJournaliers { get; set; } = new List<SuiviJournalier>();

    /// <summary>Numéro d'immatriculation CNSS du travailleur.</summary>
    [MaxLength(50)]
    public string? NumCnss { get; set; }

    /// <summary>Commune ou territoire d'affectation (portail CNSS e-déclaration).</summary>
    [MaxLength(100)]
    public string? CommuneAffectation { get; set; }

    /// <summary>Type travailleur CNSS : 1 = Travailleur, 2 = Assimilé.</summary>
    public int TypeTravailleurCnss { get; set; } = 1;

    [MaxLength(20)]
    public string? CodeBanque { get; set; }

    [MaxLength(100)]
    public string? LibelleBanque { get; set; }

    [MaxLength(50)]
    public string? AgenceBancaire { get; set; }

    [MaxLength(50)]
    public string? NumeroCompteBancaire { get; set; }

    [MaxLength(150)]
    public string? TitulaireCompteBancaire { get; set; }

    [MaxLength(10)]
    public string? DeviseCompteBancaire { get; set; }

    /// <summary>Montants mensuels CDF issus de la dernière fiche importée (alignement bulletin / Excel).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? ReferenceBrutImposableCnssCdf { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ReferenceIprNetCdf { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ReferenceCnssOuvrierCdf { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ReferenceInppCdf { get; set; }

    /// <summary>Montant mensuel normalisé en USD (contrat + taux de change).</summary>
    [NotMapped]
    public decimal SalaireMensuelUsd { get; set; }

    /// <summary>Montant mensuel normalisé en francs congolais (CDF).</summary>
    [NotMapped]
    public decimal SalaireMensuelCdf { get; set; }

    [NotMapped]
    public decimal SalaireJourUsd => SalaireMensuelUsd > 0 ? decimal.Round(SalaireMensuelUsd / 26m, 2) : 0m;

    [NotMapped]
    public decimal SalaireJourCdf => SalaireMensuelCdf > 0 ? decimal.Round(SalaireMensuelCdf / 26m, 2) : 0m;

    [NotMapped]
    public decimal SalaireHeureUsd => SalaireMensuelUsd > 0 ? decimal.Round(SalaireMensuelUsd / 26m / 8m, 2) : 0m;

    [NotMapped]
    public decimal SalaireHeureCdf => SalaireMensuelCdf > 0 ? decimal.Round(SalaireMensuelCdf / 26m / 8m, 2) : 0m;
}
