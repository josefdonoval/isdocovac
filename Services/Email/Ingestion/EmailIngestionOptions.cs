namespace Isdocovac.Services.Email.Ingestion;

public class EmailIngestionOptions
{
    public const string SectionName = "EmailIngestion";

    /// <summary>
    /// Gates the BackgroundService. Disable for local debug.
    /// </summary>
    public bool Enabled { get; set; } = false;

    public int PollIntervalSeconds { get; set; } = 60;

    public long MaxAttachmentSizeBytes { get; set; } = 25 * 1024 * 1024;

    /// <summary>
    /// Extensions (with leading dot, lowercase) that highlight an attachment as an invoice candidate.
    /// </summary>
    public List<string> InvoiceCandidateExtensions { get; set; } = new()
    {
        ".pdf", ".xml", ".isdoc", ".isdocx",
        ".jpg", ".jpeg", ".png", ".webp", ".heic", ".tif", ".tiff"
    };

    /// <summary>
    /// Base64-encoded 32-byte AES-GCM key. Loaded by AesGcmPasswordCipher.
    /// </summary>
    public string? EncryptionKey { get; set; }

    public int ErrorBackoffSecondsMin { get; set; } = 60;
    public int ErrorBackoffSecondsMax { get; set; } = 1800;
}
