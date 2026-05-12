using Isdocovac.Data;
using Isdocovac.Models.Email;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers.Email;

public interface IMailboxAccountProvider
{
    Task<MailboxAccount> CreateAsync(MailboxAccount account);
    Task<MailboxAccount?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<MailboxAccount>> ListByCompanyAsync(Guid companyId);
    Task<IReadOnlyList<MailboxAccount>> ListAllEnabledDueAsync(DateTime now);
    Task UpdateAsync(MailboxAccount account);
    Task UpdatePollStateAsync(Guid id, uint lastSeenUid, DateTime polledAt);
    Task RecordErrorAsync(Guid id, string errorMessage, DateTime nextRetryAt);
    Task ClearErrorAsync(Guid id);
    Task DeleteAsync(Guid id);
}

public class MailboxAccountProvider : IMailboxAccountProvider
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public MailboxAccountProvider(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<MailboxAccount> CreateAsync(MailboxAccount account)
    {
        var now = DateTime.UtcNow;
        account.Id = account.Id == Guid.Empty ? Guid.NewGuid() : account.Id;
        account.CreatedAt = now;
        account.UpdatedAt = now;

        await using var context = _contextFactory.CreateDbContext();
        context.MailboxAccounts.Add(account);
        await context.SaveChangesAsync();
        return account;
    }

    public async Task<MailboxAccount?> GetByIdAsync(Guid id)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.MailboxAccounts.FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<IReadOnlyList<MailboxAccount>> ListByCompanyAsync(Guid companyId)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.MailboxAccounts
            .Where(m => m.CompanyId == companyId)
            .OrderBy(m => m.DisplayName)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<MailboxAccount>> ListAllEnabledDueAsync(DateTime now)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.MailboxAccounts
            .Where(m => m.Enabled && (m.NextRetryAt == null || m.NextRetryAt <= now))
            .ToListAsync();
    }

    public async Task UpdateAsync(MailboxAccount account)
    {
        await using var context = _contextFactory.CreateDbContext();
        var existing = await context.MailboxAccounts.FirstOrDefaultAsync(m => m.Id == account.Id)
            ?? throw new InvalidOperationException($"MailboxAccount {account.Id} not found.");

        existing.DisplayName = account.DisplayName;
        existing.ImapHost = account.ImapHost;
        existing.ImapPort = account.ImapPort;
        existing.UseSsl = account.UseSsl;
        existing.Username = account.Username;
        if (!string.IsNullOrEmpty(account.PasswordEncrypted))
        {
            existing.PasswordEncrypted = account.PasswordEncrypted;
        }
        existing.ProcessedFolderName = account.ProcessedFolderName;
        existing.Enabled = account.Enabled;
        existing.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
    }

    public async Task UpdatePollStateAsync(Guid id, uint lastSeenUid, DateTime polledAt)
    {
        await using var context = _contextFactory.CreateDbContext();
        var existing = await context.MailboxAccounts.FindAsync(id);
        if (existing == null) return;
        existing.LastSeenUid = lastSeenUid;
        existing.LastPolledAt = polledAt;
        existing.LastErrorMessage = null;
        existing.NextRetryAt = null;
        existing.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public async Task RecordErrorAsync(Guid id, string errorMessage, DateTime nextRetryAt)
    {
        await using var context = _contextFactory.CreateDbContext();
        var existing = await context.MailboxAccounts.FindAsync(id);
        if (existing == null) return;
        existing.LastErrorMessage = errorMessage.Length > 2000 ? errorMessage[..2000] : errorMessage;
        existing.NextRetryAt = nextRetryAt;
        existing.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public async Task ClearErrorAsync(Guid id)
    {
        await using var context = _contextFactory.CreateDbContext();
        var existing = await context.MailboxAccounts.FindAsync(id);
        if (existing == null) return;
        existing.LastErrorMessage = null;
        existing.NextRetryAt = null;
        existing.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var context = _contextFactory.CreateDbContext();
        var existing = await context.MailboxAccounts.FindAsync(id);
        if (existing == null) return;
        context.MailboxAccounts.Remove(existing);
        await context.SaveChangesAsync();
    }
}
