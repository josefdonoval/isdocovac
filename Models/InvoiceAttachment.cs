using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Isdocovac.Models;

[Table("invoice_attachments")]
public class InvoiceAttachment
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid InvoiceId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Filename { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    // Either external URL (from Fakturoid) or Azure Blob reference
    [MaxLength(2048)]
    public string? ExternalUrl { get; set; }

    [MaxLength(255)]
    public string? BlobContainerName { get; set; }

    [MaxLength(1024)]
    public string? BlobName { get; set; }

    [MaxLength(2048)]
    public string? BlobUrl { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }

    [ForeignKey(nameof(InvoiceId))]
    public Invoice Invoice { get; set; } = null!;
}
