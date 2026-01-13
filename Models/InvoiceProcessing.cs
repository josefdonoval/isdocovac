using Isdocovac.Models.Enums;

namespace Isdocovac.Models;

public class InvoiceProcessing
{
    public Guid Id { get; set; }
    public Guid InvoiceUploadId { get; set; }
    public Guid? ParsedIsdocId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int Status { get; set; }
    public string? ErrorMessage { get; set; }
    public int AttemptNumber { get; set; }

    // Navigation properties
    public InvoiceUpload InvoiceUpload { get; set; } = null!;
    public ParsedIsdoc? ParsedIsdoc { get; set; }
}
