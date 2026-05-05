using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Isdocovac.Models;

[Table("share_quotes")]
public class ShareQuote
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid CompanyId { get; set; }

    [ForeignKey(nameof(CompanyId))]
    public Company Company { get; set; } = null!;

    [Required, MaxLength(32)]
    public string Symbol { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Name { get; set; }

    [Required, MaxLength(3)]
    public string Currency { get; set; } = "USD";

    [Required]
    [Column(TypeName = "decimal(18,6)")]
    public decimal LastPrice { get; set; }

    [Required]
    public DateTime FetchedAt { get; set; }

    [MaxLength(32)]
    public string? Source { get; set; }
}
