using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Isdocovac.Models;

[Table("invoice_lines")]
public class InvoiceLine
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid InvoiceId { get; set; }

    [Required]
    public int LineOrder { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; } = 1;

    [MaxLength(50)]
    public string? UnitName { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Required]
    [Column(TypeName = "decimal(5,2)")]
    public decimal VatRate { get; set; } = 0;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPriceWithoutVat { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPriceWithVat { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPriceWithoutVat { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalVat { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPriceWithVat { get; set; }

    [MaxLength(100)]
    public string? Sku { get; set; }

    [ForeignKey(nameof(InvoiceId))]
    public Invoice Invoice { get; set; } = null!;
}
