using Isdocovac.Models.Enums;

namespace Isdocovac.Models;

public class InvoiceUpload
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public InvoiceUploadStatus Status { get; set; }

    // Azure Blob Storage reference
    public string BlobContainerName { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string? BlobUrl { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public ICollection<ParsedIsdoc> ParsedIsdocs { get; set; } = new List<ParsedIsdoc>();
    public ICollection<InvoiceProcessing> Processings { get; set; } = new List<InvoiceProcessing>();
}
