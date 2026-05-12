using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Isdocovac.Models.Email;

[Table("mailbox_accounts")]
public class MailboxAccount
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid CompanyId { get; set; }

    [Required, MaxLength(255)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string ImapHost { get; set; } = string.Empty;

    [Required]
    public int ImapPort { get; set; } = 993;

    [Required]
    public bool UseSsl { get; set; } = true;

    [Required, MaxLength(255)]
    public string Username { get; set; } = string.Empty;

    // AES-GCM encrypted IMAP password / app password. Never logged, never returned by UI.
    [Required, MaxLength(2048)]
    public string PasswordEncrypted { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string ProcessedFolderName { get; set; } = "Isdocovac/Processed";

    [Required]
    public uint LastSeenUid { get; set; }

    [Required]
    public bool Enabled { get; set; } = true;

    public DateTime? LastPolledAt { get; set; }

    [MaxLength(2000)]
    public string? LastErrorMessage { get; set; }

    public DateTime? NextRetryAt { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }

    [Required]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey(nameof(CompanyId))]
    public Company Company { get; set; } = null!;
}
