using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Isdocovac.Models.Enums;

namespace Isdocovac.Models;

[Table("parsed_invoice_processings")]
public class ParsedInvoiceProcessing
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid ParsedInvoiceId { get; set; }

    [Required]
    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    [Required]
    public ProcessingStatus Status { get; set; }

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    [Required]
    public int AttemptNumber { get; set; }

    [ForeignKey(nameof(ParsedInvoiceId))]
    public ParsedInvoice ParsedInvoice { get; set; } = null!;
}
