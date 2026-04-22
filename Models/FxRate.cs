using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Isdocovac.Models;

[Table("fx_rates")]
public class FxRate
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = string.Empty;

    [Required]
    public int Amount { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,6)")]
    public decimal Rate { get; set; }

    [Required]
    public DateTime FetchedAt { get; set; }
}
