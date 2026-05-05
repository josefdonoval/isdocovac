using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Isdocovac.Models.Enums;

namespace Isdocovac.Models;

[Table("shares")]
public class Share
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid CompanyId { get; set; }

    [ForeignKey(nameof(CompanyId))]
    public Company Company { get; set; } = null!;

    [Required, MaxLength(32)]
    public string Symbol { get; set; } = string.Empty;

    [MaxLength(12)]
    public string? Isin { get; set; }

    [MaxLength(200)]
    public string? Name { get; set; }

    [Required]
    public DateTime TradeDateTime { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,6)")]
    public decimal Quantity { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,6)")]
    public decimal TradePrice { get; set; }

    [Required, MaxLength(3)]
    public string Currency { get; set; } = "USD";

    [Required]
    public Broker Broker { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Proceeds { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CommissionFee { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Basis { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? RealizedPnl { get; set; }

    [Required]
    public ShareTradeCode Code { get; set; }

    [Required]
    public DateOnly CnbDate { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,6)")]
    public decimal FxRate { get; set; }

    [Required]
    public int FxAmount { get; set; } = 1;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ProceedsCzk { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CommissionFeeCzk { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BasisCzk { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? RealizedPnlCzk { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }

    [Required]
    public DateTime UpdatedAt { get; set; }
}
