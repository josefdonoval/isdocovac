using Isdocovac.Data;
using Isdocovac.Models.Email;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers.Email;

public interface IEmailIngestionMessageProvider
{
    Task<EmailIngestionMessage?> GetByIdAsync(Guid id);
    Task<bool> ExistsAsync(Guid mailboxAccountId, string messageId);
    Task<EmailIngestionMessage> CreateAsync(EmailIngestionMessage message);
}

public class EmailIngestionMessageProvider : IEmailIngestionMessageProvider
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public EmailIngestionMessageProvider(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<EmailIngestionMessage?> GetByIdAsync(Guid id)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.EmailIngestionMessages.FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<bool> ExistsAsync(Guid mailboxAccountId, string messageId)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.EmailIngestionMessages
            .AnyAsync(m => m.MailboxAccountId == mailboxAccountId && m.MessageId == messageId);
    }

    public async Task<EmailIngestionMessage> CreateAsync(EmailIngestionMessage message)
    {
        message.Id = message.Id == Guid.Empty ? Guid.NewGuid() : message.Id;
        if (message.ProcessedAt == default) message.ProcessedAt = DateTime.UtcNow;

        await using var context = _contextFactory.CreateDbContext();
        context.EmailIngestionMessages.Add(message);
        await context.SaveChangesAsync();
        return message;
    }
}
