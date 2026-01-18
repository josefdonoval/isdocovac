using Isdocovac.Data;
using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers;

public interface IMainInvoiceProvider
{
    Task<Invoice?> GetByIdAsync(Guid invoiceId);
    Task<Invoice?> GetWithDetailsAsync(Guid invoiceId);
    Task<IEnumerable<Invoice>> GetByUserIdAsync(Guid userId, InvoiceDirection? direction = null, int page = 1, int pageSize = 50);
    Task<int> GetCountAsync(Guid userId, InvoiceDirection? direction = null);
    Task<Invoice> CreateAsync(Invoice invoice);
    Task UpdateAsync(Invoice invoice);
    Task DeleteAsync(Guid invoiceId);
}

public class MainInvoiceProvider : IMainInvoiceProvider
{
    private readonly ApplicationDbContext _context;

    public MainInvoiceProvider(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Invoice?> GetByIdAsync(Guid invoiceId)
    {
        return await _context.Invoices
            .FirstOrDefaultAsync(i => i.Id == invoiceId);
    }

    public async Task<Invoice?> GetWithDetailsAsync(Guid invoiceId)
    {
        return await _context.Invoices
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .Include(i => i.Attachments)
            .Include(i => i.User)
            .Include(i => i.SourceFakturoidInvoice)
            .Include(i => i.SourceParsedInvoice)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);
    }

    public async Task<IEnumerable<Invoice>> GetByUserIdAsync(Guid userId, InvoiceDirection? direction = null, int page = 1, int pageSize = 50)
    {
        var query = _context.Invoices
            .Where(i => i.UserId == userId);

        if (direction.HasValue)
        {
            query = query.Where(i => i.Direction == direction.Value);
        }

        return await query
            .OrderByDescending(i => i.IssuedOn ?? i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetCountAsync(Guid userId, InvoiceDirection? direction = null)
    {
        var query = _context.Invoices
            .Where(i => i.UserId == userId);

        if (direction.HasValue)
        {
            query = query.Where(i => i.Direction == direction.Value);
        }

        return await query.CountAsync();
    }

    public async Task<Invoice> CreateAsync(Invoice invoice)
    {
        invoice.Id = Guid.NewGuid();
        invoice.CreatedAt = DateTime.UtcNow;
        invoice.UpdatedAt = DateTime.UtcNow;
        invoice.ImportedAt = DateTime.UtcNow;

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();
        return invoice;
    }

    public async Task UpdateAsync(Invoice invoice)
    {
        invoice.UpdatedAt = DateTime.UtcNow;
        invoice.LastModifiedAt = DateTime.UtcNow;

        _context.Invoices.Update(invoice);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid invoiceId)
    {
        var invoice = await _context.Invoices.FindAsync(invoiceId);
        if (invoice != null)
        {
            _context.Invoices.Remove(invoice);
            await _context.SaveChangesAsync();
        }
    }
}
