using Isdocovac.Models.Email;

namespace Isdocovac.Services.Email.Ingestion;

public record FetchedAttachment(string FileName, string ContentType, byte[] Content);

public record FetchedMessage(
    uint Uid,
    string MessageId,
    string? From,
    string? Subject,
    DateTime ReceivedAt,
    IReadOnlyList<FetchedAttachment> Attachments,
    string? RawHeadersJson);

public interface IImapMailboxClient
{
    /// <summary>
    /// Connects to the mailbox, fetches messages with UID greater than <paramref name="lastSeenUid"/>,
    /// invokes <paramref name="onMessage"/> for each (caller persists). After successful processing
    /// the caller calls <see cref="MoveToProcessedAsync"/> on the same client to archive the message.
    /// Returns the highest UID seen (can equal <paramref name="lastSeenUid"/> when nothing new arrived).
    /// </summary>
    Task<uint> PullAsync(
        MailboxAccount account,
        string plaintextPassword,
        uint lastSeenUid,
        Func<FetchedMessage, Task<bool>> onMessage,
        CancellationToken cancellationToken);
}
