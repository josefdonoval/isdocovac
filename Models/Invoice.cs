using Isdocovac.Models.Enums;

namespace Isdocovac.Models;

public class Invoice
{
    // Primary Keys & Ownership
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    // Invoice Direction & Source
    public InvoiceDirection Direction { get; set; } // Inbound, Outbound
    public InvoiceSource Source { get; set; } // Fakturoid, ISDOC, Manual

    // Source References (nullable for audit trail)
    public Guid? FakturoidInvoiceId { get; set; }  // FK to FakturoidInvoice (staging)
    public Guid? ParsedInvoiceId { get; set; }      // FK to ParsedInvoice (staging)

    // Core Invoice Fields
    public string? CustomId { get; set; }
    public string Number { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty; // invoice, proforma, correction, etc.

    // Status Fields
    public string Status { get; set; } = string.Empty; // open, sent, paid, cancelled, etc.
    public bool Open { get; set; }
    public bool Sent { get; set; }
    public bool Overdue { get; set; }
    public bool Paid { get; set; }
    public bool Cancelled { get; set; }

    // Timestamp Fields
    public DateTime? IssuedOn { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? PaidOn { get; set; }
    public DateTime? DueOn { get; set; }
    public DateTime? CancelledAt { get; set; }
    public int? Due { get; set; } // Days until overdue

    // Financial Fields
    public decimal Subtotal { get; set; } // Amount without VAT
    public decimal Total { get; set; } // Amount with VAT
    public decimal RemainingAmount { get; set; } // Outstanding balance
    public string Currency { get; set; } = string.Empty; // ISO 4217
    public decimal? ExchangeRate { get; set; }
    public string? VatPriceMode { get; set; } // without_vat, from_total_with_vat

    // Client/Customer Information (for both directions)
    public string? ClientName { get; set; }
    public string? ClientStreet { get; set; }
    public string? ClientCity { get; set; }
    public string? ClientZip { get; set; }
    public string? ClientCountry { get; set; }
    public string? ClientRegistrationNo { get; set; }
    public string? ClientVatNo { get; set; }

    // Client Delivery Address
    public bool ClientHasDeliveryAddress { get; set; }
    public string? ClientDeliveryName { get; set; }
    public string? ClientDeliveryStreet { get; set; }
    public string? ClientDeliveryCity { get; set; }
    public string? ClientDeliveryZip { get; set; }
    public string? ClientDeliveryCountry { get; set; }

    // Supplier Information (our company for outbound, their company for inbound)
    public string? SupplierName { get; set; }
    public string? SupplierStreet { get; set; }
    public string? SupplierCity { get; set; }
    public string? SupplierZip { get; set; }
    public string? SupplierCountry { get; set; }
    public string? SupplierRegistrationNo { get; set; }
    public string? SupplierVatNo { get; set; }

    // Payment Symbols (Czech-specific)
    public string? VariableSymbol { get; set; }
    public string? ConstantSymbol { get; set; }
    public string? SpecificSymbol { get; set; }

    // Bank Account
    public string? BankAccount { get; set; }
    public string? Iban { get; set; }
    public string? SwiftBic { get; set; }

    // Notes and Description
    public string? Note { get; set; }
    public string? FooterNote { get; set; }
    public string? PrivateNote { get; set; }

    // Tags (JSON array stored as string)
    public string? Tags { get; set; } // jsonb

    // VAT Rates Summary (JSON stored as string)
    public string? VatRatesSummary { get; set; } // jsonb: Array of {vat_rate, base, vat}

    // Audit & Sync Tracking
    public DateTime ImportedAt { get; set; }  // When imported to Invoice table
    public DateTime? LastModifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public FakturoidInvoice? SourceFakturoidInvoice { get; set; }
    public ParsedInvoice? SourceParsedInvoice { get; set; }
    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    public ICollection<InvoicePayment> Payments { get; set; } = new List<InvoicePayment>();
    public ICollection<InvoiceAttachment> Attachments { get; set; } = new List<InvoiceAttachment>();
}
