using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Isdocovac.Models.Email;
using Isdocovac.Models.Enums;

namespace Isdocovac.Models.Inbox;

[Table("external_origin_files")]
public class ExternalOriginFile
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public ExternalFileOrigin Origin { get; set; }

    [Required, MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [Required]
    public long SizeBytes { get; set; }

    [MaxLength(255)]
    public string? BlobContainerName { get; set; }

    [MaxLength(1024)]
    public string? BlobName { get; set; }

    [Required]
    public bool Oversized { get; set; }

    [Required]
    public bool IsInvoiceCandidate { get; set; }

    [Required]
    public ExternalOriginFileStatus Status { get; set; } = ExternalOriginFileStatus.OnDesk;

    public Guid? ParsedInvoiceId { get; set; }

    public Guid? EmailIngestionMessageId { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }

    [Required]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey(nameof(CompanyId))]
    public Company Company { get; set; } = null!;

    [ForeignKey(nameof(EmailIngestionMessageId))]
    public EmailIngestionMessage? EmailIngestionMessage { get; set; }

    [ForeignKey(nameof(ParsedInvoiceId))]
    public ParsedInvoice? ParsedInvoice { get; set; }
}
