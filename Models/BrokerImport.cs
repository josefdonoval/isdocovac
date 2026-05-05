using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Isdocovac.Models.Enums;

namespace Isdocovac.Models;

[Table("broker_imports")]
public class BrokerImport
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid CompanyId { get; set; }

    [ForeignKey(nameof(CompanyId))]
    public Company Company { get; set; } = null!;

    [Required]
    public Broker Broker { get; set; }

    [Required, MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public long FileSize { get; set; }

    [Required, MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string BlobContainerName { get; set; } = string.Empty;

    [Required, MaxLength(1024)]
    public string BlobName { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string? BlobUrl { get; set; }

    [Required, MaxLength(64)]
    public string FileHash { get; set; } = string.Empty;

    [Required]
    public BrokerImportStatus Status { get; set; }

    public int RowsTotal { get; set; }
    public int RowsImported { get; set; }
    public int RowsSkipped { get; set; }
    public int RowsDuplicate { get; set; }

    [Required]
    public DateTime UploadedAt { get; set; }

    public DateTime? ParsedAt { get; set; }
    public DateTime? ImportedAt { get; set; }

    public string? ErrorMessage { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }

    [Required]
    public DateTime UpdatedAt { get; set; }
}
