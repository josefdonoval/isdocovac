using Isdocovac.Data;
using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers;

public interface IMainInvoiceProvider
{
    Task<Invoice?> GetByIdAsync(Guid invoiceId);
    Task<Invoice?> GetWithDetailsAsync(Guid invoiceId);
    Task<IEnumerable<Invoice>> GetByCompanyIdAsync(Guid companyId, InvoiceDirection? direction = null, int page = 1, int pageSize = 50);
    Task<int> GetCountAsync(Guid companyId, InvoiceDirection? direction = null);
    Task<int> GetCountForVatMonthAsync(Guid companyId, int year, int month);
    Task<int> GetNextInternalNumberSuffixAsync(Guid companyId, int year, int month);
    Task<IReadOnlyList<Invoice>> GetVatMonthInvoicesAsync(Guid companyId, int year, int month);
    Task<IReadOnlyList<Invoice>> GetVatQuarterInvoicesAsync(Guid companyId, int year, int quarter);
    Task<IReadOnlyList<Invoice>> GetInvoicesMissingRequiredDuzpAsync(Guid companyId, int year);
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
            .Include(i => i.Company)
            .Include(i => i.SourceFakturoidInvoice)
            .Include(i => i.SourceParsedInvoice)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);
    }

    public async Task<IEnumerable<Invoice>> GetByCompanyIdAsync(Guid companyId, InvoiceDirection? direction = null, int page = 1, int pageSize = 50)
    {
        var query = _context.Invoices
            .Where(i => i.CompanyId == companyId);

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

    public async Task<int> GetCountAsync(Guid companyId, InvoiceDirection? direction = null)
    {
        var query = _context.Invoices
            .Where(i => i.CompanyId == companyId);

        if (direction.HasValue)
        {
            query = query.Where(i => i.Direction == direction.Value);
        }

        return await query.CountAsync();
    }

    public async Task<int> GetCountForVatMonthAsync(Guid companyId, int year, int month)
    {
        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);

        return await _context.Invoices
            .Where(i => i.CompanyId == companyId
                && i.TaxableSupplyDate.HasValue
                && i.TaxableSupplyDate >= startDate
                && i.TaxableSupplyDate < endDate)
            .CountAsync();
    }

    public async Task<int> GetNextInternalNumberSuffixAsync(Guid companyId, int year, int month)
    {
        var prefix = $"{year:0000}{month:00}";

        // Ignore soft-delete filter so deleted rows still consume their suffix and we never reuse one.
        var existingNumbers = await _context.Invoices
            .IgnoreQueryFilters()
            .Where(i => i.CompanyId == companyId && i.InternalNumber.StartsWith(prefix))
            .Select(i => i.InternalNumber)
            .ToListAsync();

        var maxSuffix = 0;
        foreach (var number in existingNumbers)
        {
            if (number.Length > prefix.Length
                && int.TryParse(number.AsSpan(prefix.Length), out var suffix)
                && suffix > maxSuffix)
            {
                maxSuffix = suffix;
            }
        }

        return maxSuffix + 1;
    }

    public async Task<IReadOnlyList<Invoice>> GetVatMonthInvoicesAsync(Guid companyId, int year, int month)
    {
        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1);

        return await _context.Invoices
            .Where(i => i.CompanyId == companyId
                && !i.Cancelled
                && i.TaxableSupplyDate.HasValue
                && i.TaxableSupplyDate >= startDate
                && i.TaxableSupplyDate < endDate)
            .OrderBy(i => i.TaxableSupplyDate)
            .ThenBy(i => i.InternalNumber)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Invoice>> GetVatQuarterInvoicesAsync(Guid companyId, int year, int quarter)
    {
        if (quarter < 1 || quarter > 4) throw new ArgumentOutOfRangeException(nameof(quarter));
        var firstMonth = (quarter - 1) * 3 + 1;
        var startDate = new DateTime(year, firstMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(3);

        return await _context.Invoices
            .Where(i => i.CompanyId == companyId
                && !i.Cancelled
                && i.TaxableSupplyDate.HasValue
                && i.TaxableSupplyDate >= startDate
                && i.TaxableSupplyDate < endDate)
            .OrderBy(i => i.TaxableSupplyDate)
            .ThenBy(i => i.InternalNumber)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Invoice>> GetInvoicesMissingRequiredDuzpAsync(Guid companyId, int year)
    {
        var startDate = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddYears(1);

        var candidates = await _context.Invoices
            .Where(i => i.CompanyId == companyId
                && !i.Cancelled
                && !i.TaxableSupplyDate.HasValue
                && i.IssuedOn.HasValue
                && i.IssuedOn >= startDate
                && i.IssuedOn < endDate
                && (i.Direction == InvoiceDirection.Outbound
                    || (i.Direction == InvoiceDirection.Inbound && i.SupplierVatNo != null && i.SupplierVatNo != "")))
            .OrderBy(i => i.IssuedOn)
            .ThenBy(i => i.InternalNumber)
            .ToListAsync();

        return candidates;
    }

    public async Task<Invoice> CreateAsync(Invoice invoice)
    {
        if (invoice.Id == Guid.Empty)
        {
            invoice.Id = Guid.NewGuid();
        }

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
            invoice.IsDeleted = true;
            invoice.DeletedAt = DateTime.UtcNow;
            invoice.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
