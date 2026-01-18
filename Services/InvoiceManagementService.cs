using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Isdocovac.Providers;

namespace Isdocovac.Services;

public interface IInvoiceManagementService
{
    Task<IEnumerable<Invoice>> GetUserInvoicesAsync(Guid userId, InvoiceDirection? direction = null, int page = 1, int pageSize = 50);
    Task<int> GetUserInvoiceCountAsync(Guid userId, InvoiceDirection? direction = null);
    Task<Invoice?> GetInvoiceDetailsAsync(Guid invoiceId);
    Task<Invoice> CreateManualInvoiceAsync(Invoice invoice);
    Task UpdateInvoiceAsync(Invoice invoice);
    Task DeleteInvoiceAsync(Guid invoiceId);
}

public class InvoiceManagementService : IInvoiceManagementService
{
    private readonly IMainInvoiceProvider _invoiceProvider;

    public InvoiceManagementService(IMainInvoiceProvider invoiceProvider)
    {
        _invoiceProvider = invoiceProvider;
    }

    public async Task<IEnumerable<Invoice>> GetUserInvoicesAsync(Guid userId, InvoiceDirection? direction = null, int page = 1, int pageSize = 50)
    {
        return await _invoiceProvider.GetByUserIdAsync(userId, direction, page, pageSize);
    }

    public async Task<int> GetUserInvoiceCountAsync(Guid userId, InvoiceDirection? direction = null)
    {
        return await _invoiceProvider.GetCountAsync(userId, direction);
    }

    public async Task<Invoice?> GetInvoiceDetailsAsync(Guid invoiceId)
    {
        return await _invoiceProvider.GetWithDetailsAsync(invoiceId);
    }

    public async Task<Invoice> CreateManualInvoiceAsync(Invoice invoice)
    {
        invoice.Source = InvoiceSource.Manual;
        return await _invoiceProvider.CreateAsync(invoice);
    }

    public async Task UpdateInvoiceAsync(Invoice invoice)
    {
        await _invoiceProvider.UpdateAsync(invoice);
    }

    public async Task DeleteInvoiceAsync(Guid invoiceId)
    {
        await _invoiceProvider.DeleteAsync(invoiceId);
    }
}
