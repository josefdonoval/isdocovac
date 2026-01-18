using Isdocovac.Data;
using Isdocovac.Models;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers;

public interface IInvoiceAttachmentProvider
{
    Task<InvoiceAttachment?> GetByIdAsync(Guid id);
    Task<IEnumerable<InvoiceAttachment>> GetByInvoiceIdAsync(Guid invoiceId);
    Task<InvoiceAttachment> CreateAsync(InvoiceAttachment attachment);
    Task DeleteAsync(Guid id);
    Task<bool> ExistsAsync(Guid id);
}

public class InvoiceAttachmentProvider : IInvoiceAttachmentProvider
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<InvoiceAttachmentProvider> _logger;

    public InvoiceAttachmentProvider(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<InvoiceAttachmentProvider> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<InvoiceAttachment?> GetByIdAsync(Guid id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.InvoiceAttachments
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IEnumerable<InvoiceAttachment>> GetByInvoiceIdAsync(Guid invoiceId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.InvoiceAttachments
            .Where(a => a.InvoiceId == invoiceId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<InvoiceAttachment> CreateAsync(InvoiceAttachment attachment)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        if (attachment.Id == Guid.Empty)
        {
            attachment.Id = Guid.NewGuid();
        }

        attachment.CreatedAt = DateTime.UtcNow;

        context.InvoiceAttachments.Add(attachment);
        await context.SaveChangesAsync();

        _logger.LogInformation("Created invoice attachment {AttachmentId} for invoice {InvoiceId}",
            attachment.Id, attachment.InvoiceId);

        return attachment;
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var attachment = await context.InvoiceAttachments.FindAsync(id);
        if (attachment != null)
        {
            context.InvoiceAttachments.Remove(attachment);
            await context.SaveChangesAsync();

            _logger.LogInformation("Deleted invoice attachment {AttachmentId}", id);
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.InvoiceAttachments.AnyAsync(a => a.Id == id);
    }
}
