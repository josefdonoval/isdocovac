using Isdocovac.Models.Email;
using Isdocovac.Models.Enums;
using Isdocovac.Models.Inbox;
using Isdocovac.Providers;
using Isdocovac.Providers.Email;
using Isdocovac.Providers.Inbox;
using Isdocovac.Services.Security;
using Microsoft.Extensions.Options;

namespace Isdocovac.Services.Email.Ingestion;

public interface IEmailIngestionService
{
    /// <summary>
    /// Pulls new messages from a single mailbox once and persists attachments to the desk.
    /// Does not call any paid AI service — only blob upload + DB writes.
    /// </summary>
    Task<int> PullMailboxOnceAsync(Guid mailboxAccountId, CancellationToken cancellationToken = default);
}

public class EmailIngestionService : IEmailIngestionService
{
    private readonly IMailboxAccountProvider _mailboxes;
    private readonly IEmailIngestionMessageProvider _messages;
    private readonly IExternalOriginFileProvider _files;
    private readonly IAzureBlobStorageProvider _blob;
    private readonly IPasswordCipher _cipher;
    private readonly IImapMailboxClient _imap;
    private readonly EmailIngestionOptions _options;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailIngestionService> _logger;

    public EmailIngestionService(
        IMailboxAccountProvider mailboxes,
        IEmailIngestionMessageProvider messages,
        IExternalOriginFileProvider files,
        IAzureBlobStorageProvider blob,
        IPasswordCipher cipher,
        IImapMailboxClient imap,
        IOptions<EmailIngestionOptions> options,
        IConfiguration configuration,
        ILogger<EmailIngestionService> logger)
    {
        _mailboxes = mailboxes;
        _messages = messages;
        _files = files;
        _blob = blob;
        _cipher = cipher;
        _imap = imap;
        _options = options.Value;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<int> PullMailboxOnceAsync(Guid mailboxAccountId, CancellationToken cancellationToken = default)
    {
        var account = await _mailboxes.GetByIdAsync(mailboxAccountId);
        if (account == null || !account.Enabled)
        {
            _logger.LogInformation("Mailbox {MailboxId} not found or disabled; skipping.", mailboxAccountId);
            return 0;
        }

        string password;
        try
        {
            password = _cipher.Decrypt(account.PasswordEncrypted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt password for mailbox {MailboxId}.", account.Id);
            await _mailboxes.RecordErrorAsync(account.Id,
                "Failed to decrypt stored password (encryption key may have rotated).",
                DateTime.UtcNow.AddSeconds(_options.ErrorBackoffSecondsMax));
            return 0;
        }

        var ingested = 0;
        try
        {
            var newWatermark = await _imap.PullAsync(account, password, account.LastSeenUid,
                async fetched => await ProcessMessageAsync(account, fetched, cancellationToken),
                cancellationToken);

            await _mailboxes.UpdatePollStateAsync(account.Id, newWatermark, DateTime.UtcNow);
            ingested = (int)Math.Max(0, (long)newWatermark - (long)account.LastSeenUid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IMAP pull failed for mailbox {MailboxId}.", account.Id);
            var backoff = TimeSpan.FromSeconds(_options.ErrorBackoffSecondsMin);
            await _mailboxes.RecordErrorAsync(account.Id, ex.Message, DateTime.UtcNow.Add(backoff));
        }

        return ingested;
    }

    private async Task<bool> ProcessMessageAsync(MailboxAccount account, FetchedMessage fetched, CancellationToken cancellationToken)
    {
        if (await _messages.ExistsAsync(account.Id, fetched.MessageId))
        {
            // Already ingested in a previous run — caller still moves to processed folder.
            return true;
        }

        var message = await _messages.CreateAsync(new EmailIngestionMessage
        {
            MailboxAccountId = account.Id,
            CompanyId = account.CompanyId,
            ImapUid = fetched.Uid,
            MessageId = fetched.MessageId,
            From = Truncate(fetched.From, 500),
            Subject = Truncate(fetched.Subject, 998),
            ReceivedAt = EnsureUtc(fetched.ReceivedAt),
            RawHeadersJson = fetched.RawHeadersJson,
            ProcessedAt = DateTime.UtcNow
        });

        var containerName = _configuration["AzureStorage:InvoiceContainerName"] ?? "invoice-uploads";

        foreach (var attachment in fetched.Attachments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var oversized = attachment.Content.LongLength > _options.MaxAttachmentSizeBytes;
            string? blobName = null;
            string? blobContainer = null;
            var fileId = Guid.NewGuid();

            if (!oversized)
            {
                blobContainer = containerName;
                blobName = $"inbox/{account.CompanyId}/{fileId}/{SanitizeFileName(attachment.FileName)}";
                try
                {
                    using var stream = new MemoryStream(attachment.Content);
                    await _blob.UploadBlobAsync(blobContainer, blobName, stream, attachment.ContentType);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Blob upload failed for attachment {FileName} from mailbox {MailboxId}.",
                        attachment.FileName, account.Id);
                    return false;
                }
            }

            await _files.CreateAsync(new ExternalOriginFile
            {
                Id = fileId,
                CompanyId = account.CompanyId,
                Origin = ExternalFileOrigin.Email,
                FileName = Truncate(attachment.FileName, 500) ?? "attachment",
                ContentType = Truncate(attachment.ContentType, 100) ?? "application/octet-stream",
                SizeBytes = attachment.Content.LongLength,
                BlobContainerName = blobContainer,
                BlobName = blobName,
                Oversized = oversized,
                IsInvoiceCandidate = IsInvoiceCandidate(attachment.FileName),
                Status = ExternalOriginFileStatus.OnDesk,
                EmailIngestionMessageId = message.Id
            });
        }

        return true;
    }

    private bool IsInvoiceCandidate(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return _options.InvoiceCandidateExtensions
            .Any(e => string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(fileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "attachment" : cleaned;
    }

    private static string? Truncate(string? input, int maxLength)
    {
        if (input == null) return null;
        return input.Length <= maxLength ? input : input[..maxLength];
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
