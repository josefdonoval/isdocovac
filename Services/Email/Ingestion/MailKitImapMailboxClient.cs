using System.Text.Json;
using Isdocovac.Models.Email;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;

namespace Isdocovac.Services.Email.Ingestion;

public class MailKitImapMailboxClient : IImapMailboxClient
{
    private readonly ILogger<MailKitImapMailboxClient> _logger;

    public MailKitImapMailboxClient(ILogger<MailKitImapMailboxClient> logger)
    {
        _logger = logger;
    }

    public async Task<uint> PullAsync(
        MailboxAccount account,
        string plaintextPassword,
        uint lastSeenUid,
        Func<FetchedMessage, Task<bool>> onMessage,
        CancellationToken cancellationToken)
    {
        using var client = new ImapClient();

        var socketOptions = account.UseSsl
            ? MailKit.Security.SecureSocketOptions.SslOnConnect
            : MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable;

        await client.ConnectAsync(account.ImapHost, account.ImapPort, socketOptions, cancellationToken);
        await client.AuthenticateAsync(account.Username, plaintextPassword, cancellationToken);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadWrite, cancellationToken);

        // Resolve / create the processed folder up-front so MoveTo can succeed later.
        var processedFolder = await EnsureProcessedFolderAsync(client, account.ProcessedFolderName, cancellationToken);

        var nextUid = lastSeenUid == 0 ? 1u : lastSeenUid + 1;
        var query = SearchQuery.Uids(new UniqueIdRange(new UniqueId(nextUid), UniqueId.MaxValue));
        var uids = await inbox.SearchAsync(query, cancellationToken);

        var highestUid = lastSeenUid;

        foreach (var uid in uids.OrderBy(u => u.Id))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (uid.Id <= lastSeenUid) continue;

            MimeMessage message;
            try
            {
                message = await inbox.GetMessageAsync(uid, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch IMAP UID {Uid} from {MailboxId}; skipping.", uid.Id, account.Id);
                highestUid = Math.Max(highestUid, uid.Id);
                continue;
            }

            var attachments = new List<FetchedAttachment>();
            foreach (var part in message.Attachments)
            {
                if (part is not MimePart mimePart) continue;
                using var ms = new MemoryStream();
                await mimePart.Content.DecodeToAsync(ms, cancellationToken);
                var fileName = mimePart.FileName ?? mimePart.ContentType?.Name ?? "attachment";
                var contentType = mimePart.ContentType?.MimeType ?? "application/octet-stream";
                attachments.Add(new FetchedAttachment(fileName, contentType, ms.ToArray()));
            }

            var headersJson = JsonSerializer.Serialize(
                message.Headers.Select(h => new { Name = h.Field, Value = h.Value }));

            var fetched = new FetchedMessage(
                Uid: uid.Id,
                MessageId: string.IsNullOrEmpty(message.MessageId)
                    ? $"<no-id-{uid.Id}@{account.ImapHost}>"
                    : message.MessageId,
                From: message.From?.ToString(),
                Subject: message.Subject,
                ReceivedAt: message.Date.UtcDateTime,
                Attachments: attachments,
                RawHeadersJson: headersJson);

            bool processed;
            try
            {
                processed = await onMessage(fetched);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Message handler threw for UID {Uid} in mailbox {MailboxId}.", uid.Id, account.Id);
                processed = false;
            }

            if (processed)
            {
                try
                {
                    await inbox.MoveToAsync(uid, processedFolder, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Could not MOVE UID {Uid} to processed folder for mailbox {MailboxId}. " +
                        "Message persists in DB; idempotency relies on Message-Id dedup.", uid.Id, account.Id);
                }
                highestUid = Math.Max(highestUid, uid.Id);
            }
            else
            {
                // Stop advancing the UID watermark on first failure so we retry next poll.
                break;
            }
        }

        await client.DisconnectAsync(true, cancellationToken);
        return highestUid;
    }

    private static async Task<IMailFolder> EnsureProcessedFolderAsync(
        ImapClient client, string folderPath, CancellationToken cancellationToken)
    {
        // Folder paths use the server's hierarchy delimiter (Gmail uses '/').
        var personal = client.GetFolder(client.PersonalNamespaces[0]);
        var delimiter = personal.DirectorySeparator;

        var segments = folderPath.Split(delimiter, StringSplitOptions.RemoveEmptyEntries);
        var current = personal;
        foreach (var segment in segments)
        {
            IMailFolder next;
            try
            {
                next = await current.GetSubfolderAsync(segment, cancellationToken);
            }
            catch (FolderNotFoundException)
            {
                next = await current.CreateAsync(segment, isMessageFolder: true, cancellationToken);
            }
            current = next;
        }
        return current;
    }
}
