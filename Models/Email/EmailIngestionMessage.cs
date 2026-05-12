using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Isdocovac.Models.Email;

[Table("email_ingestion_messages")]
public class EmailIngestionMessage
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid MailboxAccountId { get; set; }

    // Denormalized for quick per-company filtering and joins from the desk.
    [Required]
    public Guid CompanyId { get; set; }

    [Required]
    public uint ImapUid { get; set; }

    // RFC822 Message-Id header. Unique per mailbox to prevent duplicates if UIDs reset.
    [Required, MaxLength(998)]
    public string MessageId { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? From { get; set; }

    [MaxLength(998)]
    public string? Subject { get; set; }

    [Required]
    public DateTime ReceivedAt { get; set; }

    [Column(TypeName = "jsonb")]
    public string? RawHeadersJson { get; set; }

    [Required]
    public DateTime ProcessedAt { get; set; }

    [ForeignKey(nameof(MailboxAccountId))]
    public MailboxAccount MailboxAccount { get; set; } = null!;
}
