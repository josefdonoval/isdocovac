namespace Isdocovac.Models;

public class FakturoidInvoiceAttachment
{
    public Guid Id { get; set; }
    public Guid FakturoidInvoiceId { get; set; }

    // Attachment metadata from Fakturoid
    public string Filename { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;

    // Sync tracking
    public DateTime ImportedAt { get; set; }

    // Navigation properties
    public FakturoidInvoice Invoice { get; set; } = null!;
}
