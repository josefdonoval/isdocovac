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
    public string RawXmlContent { get; set; } = string.Empty;

    // Navigation properties
    public User User { get; set; } = null!;
    public ICollection<ParsedIsdoc> ParsedIsdocs { get; set; } = new List<ParsedIsdoc>();
}
