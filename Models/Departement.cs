using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MelodyPaieRDC.Models;

/// <summary>
/// Département d'un établissement.
/// </summary>
public class Departement
{
    [Key]
    public int Id { get; set; }

    [ForeignKey(nameof(Etablissement))]
    public int EtablissementId { get; set; }

    public Etablissement? Etablissement { get; set; }

    [Required]
    [MaxLength(255)]
    public string NomDepartement { get; set; } = string.Empty;

    public ICollection<Employe> Employes { get; set; } = new List<Employe>();
}

