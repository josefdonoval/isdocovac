namespace Isdocovac.Models;

public class InvoiceAttachment
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }

    public string Filename { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;

    // Either external URL (from Fakturoid) or Azure Blob reference
    public string? ExternalUrl { get; set; }
    public string? BlobContainerName { get; set; }
    public string? BlobName { get; set; }
    public string? BlobUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public Invoice Invoice { get; set; } = null!;
}
