namespace Isdocovac.Models;

public class FakturoidInvoice
{
    public Guid Id { get; set; }
    public Guid FakturoidConnectionId { get; set; }

    // Fakturoid Core Fields (Read-only from API)
    public int FakturoidId { get; set; } // Primary ID from Fakturoid
    public string? CustomId { get; set; }
    public string Number { get; set; } = string.Empty;
    public string? Token { get; set; } // Security token for public links
    public string DocumentType { get; set; } = string.Empty; // invoice, proforma, correction, tax_document, final_invoice

    // Status Fields
    public string Status { get; set; } = string.Empty; // open, sent, overdue, paid, cancelled, uncollectible
    public bool Open { get; set; }
    public bool Sent { get; set; }
    public bool Overdue { get; set; }
    public bool Paid { get; set; }
    public bool Cancelled { get; set; }
    public bool Uncollectible { get; set; }

    // Timestamp Fields
    public DateTime? IssuedOn { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? PaidOn { get; set; }
    public DateTime? DueOn { get; set; }
    public DateTime? LockedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? UncollectibleAt { get; set; }
    public DateTime? CreatedAt { get; set; } // From Fakturoid
    public DateTime? UpdatedAt { get; set; } // From Fakturoid

    // Financial Fields
    public decimal Subtotal { get; set; } // Amount without VAT
    public decimal Total { get; set; } // Amount with VAT
    public decimal RemainingAmount { get; set; } // Outstanding balance
    public string Currency { get; set; } = string.Empty; // ISO 4217 code
    public decimal? ExchangeRate { get; set; }
    public decimal? NativeSubtotal { get; set; } // In account currency
    public decimal? NativeTotal { get; set; }
    public decimal? NativeRemainingAmount { get; set; }
    public string? VatPriceMode { get; set; } // without_vat, from_total_with_vat

    // Due Date Fields
    public int? Due { get; set; } // Days until overdue

    // Client Information
    public int? SubjectId { get; set; } // Fakturoid subject ID
    public string? SubjectCustomId { get; set; }
    public string? ClientName { get; set; }
    public string? ClientStreet { get; set; }
    public string? ClientCity { get; set; }
    public string? ClientZip { get; set; }
    public string? ClientCountry { get; set; }
    public string? ClientRegistrationNo { get; set; }
    public string? ClientVatNo { get; set; }
    public string? ClientLocalVatNo { get; set; }

    // Client Delivery Address
    public bool ClientHasDeliveryAddress { get; set; }
    public string? ClientDeliveryName { get; set; }
    public string? ClientDeliveryStreet { get; set; }
    public string? ClientDeliveryCity { get; set; }
    public string? ClientDeliveryZip { get; set; }
    public string? ClientDeliveryCountry { get; set; }

    // Your Company Information (Read-only)
    public string? YourName { get; set; }
    public string? YourStreet { get; set; }
    public string? YourCity { get; set; }
    public string? YourZip { get; set; }
    public string? YourCountry { get; set; }
    public string? YourRegistrationNo { get; set; }
    public string? YourVatNo { get; set; }
    public string? YourLocalVatNo { get; set; }

    // Additional Fields
    public string? VariableSymbol { get; set; }
    public string? ConstantSymbol { get; set; }
    public string? SpecificSymbol { get; set; }
    public int? NumberFormatId { get; set; }
    public int? GeneratorId { get; set; }
    public int? RelatedId { get; set; }
    public int? CorrectionId { get; set; }
    public string? ProformaFollowupDocument { get; set; }

    // Payment Integration
    public string? Paypal { get; set; }
    public string? Gopay { get; set; }

    // Notes and Description
    public string? Note { get; set; }
    public string? FooterNote { get; set; }
    public string? PrivateNote { get; set; }

    // Bank Account
    public int? BankAccountId { get; set; }
    public string? BankAccount { get; set; }
    public string? Iban { get; set; }
    public string? SwiftBic { get; set; }

    // Tags (JSON array stored as string)
    public string? Tags { get; set; }

    // VAT Rates Summary (JSON stored as string)
    public string? VatRatesSummary { get; set; } // Array of {vat_rate, base, vat}
    public string? NativeVatRatesSummary { get; set; }

    // Paid Advances (JSON stored as string)
    public string? PaidAdvances { get; set; } // Array of tax documents

    // HTML URL for viewing in Fakturoid
    public string? HtmlUrl { get; set; }
    public string? PublicHtmlUrl { get; set; }

    // Sync Tracking
    public DateTime ImportedAt { get; set; } // When first imported to our DB
    public DateTime LastSyncedAt { get; set; } // Last time we synced from Fakturoid
    public DateTime? FakturoidUpdatedAt { get; set; } // updated_at from Fakturoid API

    // Navigation properties
    public FakturoidConnection Connection { get; set; } = null!;
    public ICollection<FakturoidInvoiceLine> Lines { get; set; } = new List<FakturoidInvoiceLine>();
    public ICollection<FakturoidInvoicePayment> Payments { get; set; } = new List<FakturoidInvoicePayment>();
    public ICollection<FakturoidInvoiceAttachment> Attachments { get; set; } = new List<FakturoidInvoiceAttachment>();
}
