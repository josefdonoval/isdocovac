namespace Isdocovac.Models;

public class FakturoidInvoicePayment
{
    public Guid Id { get; set; }
    public Guid FakturoidInvoiceId { get; set; }

    // Payment Fields from Fakturoid
    public int FakturoidPaymentId { get; set; }
    public DateTime PaidOn { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? NativeAmount { get; set; }
    public string? VariableSymbol { get; set; }
    public int? BankAccountId { get; set; }
    public int? TaxDocumentId { get; set; }

    // Timestamps from Fakturoid
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Sync tracking
    public DateTime ImportedAt { get; set; }

    // Navigation properties
    public FakturoidInvoice Invoice { get; set; } = null!;
}
