using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Isdocovac.Models.Enums;

namespace Isdocovac.Models;

[Table("parsed_invoices")]
public class ParsedInvoice
{
    // Primary Keys & Ownership
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    // Original Upload Information (from InvoiceUpload)
    [Required]
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public long FileSize { get; set; }

    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [Required]
    public DateTime UploadedAt { get; set; }

    // Azure Blob Storage reference (original XML file)
    [Required]
    [MaxLength(255)]
    public string BlobContainerName { get; set; } = string.Empty;

    [Required]
    [MaxLength(1024)]
    public string BlobName { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string? BlobUrl { get; set; }

    // Parsing Status
    [Required]
    public ParsedInvoiceStatus Status { get; set; }

    public DateTime? ParsedAt { get; set; }

    [Required]
    public bool IsValid { get; set; }

    public string? ValidationErrors { get; set; }

    // Parsed Invoice Data (comprehensive fields from ISDOC)
    [MaxLength(100)]
    public string? InvoiceNumber { get; set; }

    [MaxLength(100)]
    public string? CustomId { get; set; }

    public DateTime? IssuedOn { get; set; }

    public DateTime? DueOn { get; set; }

    [MaxLength(50)]
    public string? DocumentType { get; set; }

    // Financial Fields
    [Column(TypeName = "decimal(18,2)")]
    public decimal? Subtotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Total { get; set; }

    [MaxLength(10)]
    public string? Currency { get; set; }

    [MaxLength(50)]
    public string? VatPriceMode { get; set; }

    // Supplier (from ISDOC AccountingSupplierParty)
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

    // Customer (from ISDOC AccountingCustomerParty)
    [MaxLength(500)]
    public string? CustomerName { get; set; }

    [MaxLength(500)]
    public string? CustomerStreet { get; set; }

    [MaxLength(255)]
    public string? CustomerCity { get; set; }

    [MaxLength(20)]
    public string? CustomerZip { get; set; }

    [MaxLength(100)]
    public string? CustomerCountry { get; set; }

    [MaxLength(100)]
    public string? CustomerRegistrationNo { get; set; }

    [MaxLength(100)]
    public string? CustomerVatNo { get; set; }

    // Payment Information
    [MaxLength(50)]
    public string? VariableSymbol { get; set; }

    [MaxLength(50)]
    public string? ConstantSymbol { get; set; }

    [MaxLength(50)]
    public string? SpecificSymbol { get; set; }

    [MaxLength(100)]
    public string? BankAccount { get; set; }

    [MaxLength(100)]
    public string? Iban { get; set; }

    // Notes
    public string? Note { get; set; }

    // Full parsed data as JSON (for reference/re-parsing)
    [Column(TypeName = "jsonb")]
    public string? ParsedDataJson { get; set; }

    // Line items as JSON (simplified storage for staging)
    [Column(TypeName = "jsonb")]
    public string? LinesJson { get; set; }

    // VAT summary as JSON
    [Column(TypeName = "jsonb")]
    public string? VatRatesSummary { get; set; }

    // Import Tracking
    public DateTime? ImportedToInvoiceAt { get; set; }

    public Guid? ImportedInvoiceId { get; set; }

    // Audit
    [Required]
    public DateTime CreatedAt { get; set; }

    [Required]
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(ImportedInvoiceId))]
    public Invoice? ImportedInvoice { get; set; }

    public ICollection<ParsedInvoiceProcessing> Processings { get; set; } = new List<ParsedInvoiceProcessing>();
}
