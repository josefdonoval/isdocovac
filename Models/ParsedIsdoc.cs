namespace Isdocovac.Models;

public class ParsedIsdoc
{
    public Guid Id { get; set; }
    public Guid InvoiceUploadId { get; set; }
    public DateTime ParsedAt { get; set; }
    public bool IsValid { get; set; }
    public string? ValidationErrors { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? SupplierName { get; set; }
    public string? CustomerName { get; set; }
    public decimal? TotalAmount { get; set; }
    public string? Currency { get; set; }
    public string ParsedData { get; set; } = string.Empty;

    // Navigation properties
    public InvoiceUpload InvoiceUpload { get; set; } = null!;
}
