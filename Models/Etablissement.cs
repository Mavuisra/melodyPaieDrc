using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Etablissement / site de l'entreprise (différentes provinces, sites, etc.).
/// </summary>
public class Etablissement
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Entreprise))]
    public int EntrepriseId { get; set; }

    public Entreprise? Entreprise { get; set; }

    [Required]
    [MaxLength(255)]
    public string NomSite { get; set; } = string.Empty;

    public ICollection<Departement> Departements { get; set; } = new List<Departement>();
}

