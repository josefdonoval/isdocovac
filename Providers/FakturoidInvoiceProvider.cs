using Isdocovac.Data;
using Isdocovac.Models;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers;

public interface IFakturoidInvoiceProvider
{
    Task<FakturoidInvoice?> GetByIdAsync(Guid invoiceId);
    Task<FakturoidInvoice?> GetByFakturoidIdAsync(Guid connectionId, int fakturoidId);
    Task<IEnumerable<FakturoidInvoice>> GetByConnectionIdAsync(Guid connectionId, int page = 1, int pageSize = 40);
    Task<FakturoidInvoice> CreateOrUpdateAsync(Guid connectionId, FakturoidInvoice invoice);
    Task DeleteAsync(Guid invoiceId);
    Task<int> GetTotalCountAsync(Guid connectionId);
    Task<DateTime?> GetLastSyncedTimeAsync(Guid connectionId);
}

public class FakturoidInvoiceProvider : IFakturoidInvoiceProvider
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public FakturoidInvoiceProvider(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<FakturoidInvoice?> GetByIdAsync(Guid invoiceId)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.FakturoidInvoices
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .Include(i => i.Attachments)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);
    }

    public async Task<FakturoidInvoice?> GetByFakturoidIdAsync(Guid connectionId, int fakturoidId)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.FakturoidInvoices
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .Include(i => i.Attachments)
            .FirstOrDefaultAsync(i => i.FakturoidConnectionId == connectionId && i.FakturoidId == fakturoidId);
    }

    public async Task<IEnumerable<FakturoidInvoice>> GetByConnectionIdAsync(Guid connectionId, int page = 1, int pageSize = 40)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.FakturoidInvoices
            .Where(i => i.FakturoidConnectionId == connectionId)
            .OrderByDescending(i => i.IssuedOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<FakturoidInvoice> CreateOrUpdateAsync(Guid connectionId, FakturoidInvoice invoice)
    {
        await using var context = _contextFactory.CreateDbContext();

        var existing = await context.FakturoidInvoices
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .Include(i => i.Attachments)
            .FirstOrDefaultAsync(i =>
                i.FakturoidConnectionId == connectionId &&
                i.FakturoidId == invoice.FakturoidId);

        if (existing == null)
        {
            // Create new
            invoice.Id = Guid.NewGuid();
            invoice.FakturoidConnectionId = connectionId;
            invoice.ImportedAt = DateTime.UtcNow;
            invoice.LastSyncedAt = DateTime.UtcNow;
            context.FakturoidInvoices.Add(invoice);
        }
        else
        {
            // Update existing - preserve ImportedAt
            invoice.Id = existing.Id;
            invoice.FakturoidConnectionId = connectionId;
            invoice.ImportedAt = existing.ImportedAt;
            invoice.LastSyncedAt = DateTime.UtcNow;

            // Remove old related entities
            context.FakturoidInvoiceLines.RemoveRange(existing.Lines);
            context.FakturoidInvoicePayments.RemoveRange(existing.Payments);
            context.FakturoidInvoiceAttachments.RemoveRange(existing.Attachments);

            // Update invoice
            context.Entry(existing).CurrentValues.SetValues(invoice);

            // Add new related entities
            foreach (var line in invoice.Lines)
            {
                line.FakturoidInvoiceId = invoice.Id;
                context.FakturoidInvoiceLines.Add(line);
            }
            foreach (var payment in invoice.Payments)
            {
                payment.FakturoidInvoiceId = invoice.Id;
                context.FakturoidInvoicePayments.Add(payment);
            }
            foreach (var attachment in invoice.Attachments)
            {
                attachment.FakturoidInvoiceId = invoice.Id;
                context.FakturoidInvoiceAttachments.Add(attachment);
            }
        }

        await context.SaveChangesAsync();
        return invoice;
    }

    public async Task DeleteAsync(Guid invoiceId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var invoice = await context.FakturoidInvoices.FindAsync(invoiceId);
        if (invoice != null)
        {
            context.FakturoidInvoices.Remove(invoice);
            await context.SaveChangesAsync();
        }
    }

    public async Task<int> GetTotalCountAsync(Guid connectionId)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.FakturoidInvoices
            .CountAsync(i => i.FakturoidConnectionId == connectionId);
    }

    public async Task<DateTime?> GetLastSyncedTimeAsync(Guid connectionId)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.FakturoidInvoices
            .Where(i => i.FakturoidConnectionId == connectionId)
            .MaxAsync(i => (DateTime?)i.LastSyncedAt);
    }
}
