using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Isdocovac.Models.Enums;

namespace Isdocovac.Models;

[Table("invoices")]
public class Invoice
{
    // Primary Keys & Ownership
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    // Invoice Direction & Source
    [Required]
    public InvoiceDirection Direction { get; set; } // Inbound, Outbound

    [Required]
    public InvoiceSource Source { get; set; } // Fakturoid, ISDOC, Manual

    // Source References (nullable for audit trail)
    public Guid? FakturoidInvoiceId { get; set; }  // FK to FakturoidInvoice (staging)
    public Guid? ParsedInvoiceId { get; set; }      // FK to ParsedInvoice (staging)

    // Core Invoice Fields
    [MaxLength(100)]
    public string? CustomId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Number { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string DocumentType { get; set; } = string.Empty; // invoice, proforma, correction, etc.

    // Status Fields
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = string.Empty; // open, sent, paid, cancelled, etc.

    [Required]
    public bool Open { get; set; }

    [Required]
    public bool Sent { get; set; }

    [Required]
    public bool Overdue { get; set; }

    [Required]
    public bool Paid { get; set; }

    [Required]
    public bool Cancelled { get; set; }

    // Timestamp Fields
    public DateTime? IssuedOn { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? PaidOn { get; set; }
    public DateTime? DueOn { get; set; }
    public DateTime? CancelledAt { get; set; }
    public int? Due { get; set; } // Days until overdue

    // Financial Fields
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Subtotal { get; set; } // Amount without VAT

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; } // Amount with VAT

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal RemainingAmount { get; set; } // Outstanding balance

    [Required]
    [MaxLength(10)]
    public string Currency { get; set; } = string.Empty; // ISO 4217

    [Column(TypeName = "decimal(18,6)")]
    public decimal? ExchangeRate { get; set; }

    [MaxLength(50)]
    public string? VatPriceMode { get; set; } // without_vat, from_total_with_vat

    // Client/Customer Information (for both directions)
    [MaxLength(500)]
    public string? ClientName { get; set; }

    [MaxLength(500)]
    public string? ClientStreet { get; set; }

    [MaxLength(255)]
    public string? ClientCity { get; set; }

    [MaxLength(20)]
    public string? ClientZip { get; set; }

    [MaxLength(100)]
    public string? ClientCountry { get; set; }

    [MaxLength(100)]
    public string? ClientRegistrationNo { get; set; }

    [MaxLength(100)]
    public string? ClientVatNo { get; set; }

    // Client Delivery Address
    [Required]
    public bool ClientHasDeliveryAddress { get; set; }

    [MaxLength(500)]
    public string? ClientDeliveryName { get; set; }

    [MaxLength(500)]
    public string? ClientDeliveryStreet { get; set; }

    [MaxLength(255)]
    public string? ClientDeliveryCity { get; set; }

    [MaxLength(20)]
    public string? ClientDeliveryZip { get; set; }

    [MaxLength(100)]
    public string? ClientDeliveryCountry { get; set; }

    // Supplier Information (our company for outbound, their company for inbound)
    [MaxLength(500)]
    public string? SupplierName { get; set; }

    [MaxLength(500)]
    public string? SupplierStreet { get; set; }

    [MaxLength(255)]
    public string? SupplierCity { get; set; }

    [MaxLength(20)]
    public string? SupplierZip { get; set; }

    [MaxLength(100)]
    public string? SupplierCountry { get; set; }

    [MaxLength(100)]
    public string? SupplierRegistrationNo { get; set; }

    [MaxLength(100)]
    public string? SupplierVatNo { get; set; }

    // Payment Symbols (Czech-specific)
    [MaxLength(50)]
    public string? VariableSymbol { get; set; }

    [MaxLength(50)]
    public string? ConstantSymbol { get; set; }

    [MaxLength(50)]
    public string? SpecificSymbol { get; set; }

    // Bank Account
    [MaxLength(100)]
    public string? BankAccount { get; set; }

    [MaxLength(100)]
    public string? Iban { get; set; }

    [MaxLength(100)]
    public string? SwiftBic { get; set; }

    // Notes and Description
    public string? Note { get; set; }
    public string? FooterNote { get; set; }
    public string? PrivateNote { get; set; }

    // Tags (JSON array stored as string)
    [Column(TypeName = "jsonb")]
    public string? Tags { get; set; }

    // VAT Rates Summary (JSON stored as string)
    [Column(TypeName = "jsonb")]
    public string? VatRatesSummary { get; set; } // Array of {vat_rate, base, vat}

    // Audit & Sync Tracking
    [Required]
    public DateTime ImportedAt { get; set; }  // When imported to Invoice table

    public DateTime? LastModifiedAt { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }

    [Required]
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(FakturoidInvoiceId))]
    public FakturoidInvoice? SourceFakturoidInvoice { get; set; }

    [ForeignKey(nameof(ParsedInvoiceId))]
    public ParsedInvoice? SourceParsedInvoice { get; set; }

    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    public ICollection<InvoicePayment> Payments { get; set; } = new List<InvoicePayment>();
    public ICollection<InvoiceAttachment> Attachments { get; set; } = new List<InvoiceAttachment>();
}
