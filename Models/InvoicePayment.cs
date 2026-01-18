using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Isdocovac.Models;

[Table("invoice_payments")]
public class InvoicePayment
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid InvoiceId { get; set; }

    [Required]
    public DateTime PaidOn { get; set; }

    [Required]
    [MaxLength(10)]
    public string Currency { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(50)]
    public string? VariableSymbol { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }

    [ForeignKey(nameof(InvoiceId))]
    public Invoice Invoice { get; set; } = null!;
}
