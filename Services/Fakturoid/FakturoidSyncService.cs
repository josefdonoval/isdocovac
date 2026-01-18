using Isdocovac.Providers;

namespace Isdocovac.Services.Fakturoid;

public interface IFakturoidSyncService
{
    Task<int> SyncInvoicesAsync(Guid userId, bool fullSync = false);
    Task<SyncResult> GetSyncStatusAsync(Guid userId);
}

public class SyncResult
{
    public int TotalInvoices { get; set; }
    public int NewInvoices { get; set; }
    public int UpdatedInvoices { get; set; }
    public DateTime? LastSyncedAt { get; set; }
}

public class FakturoidSyncService : IFakturoidSyncService
{
    private readonly IFakturoidConnectionProvider _connectionProvider;
    private readonly IFakturoidInvoiceProvider _invoiceProvider;
    private readonly IFakturoidApiService _apiService;
    private readonly ILogger<FakturoidSyncService> _logger;

    public FakturoidSyncService(
        IFakturoidConnectionProvider connectionProvider,
        IFakturoidInvoiceProvider invoiceProvider,
        IFakturoidApiService apiService,
        ILogger<FakturoidSyncService> logger)
    {
        _connectionProvider = connectionProvider;
        _invoiceProvider = invoiceProvider;
        _apiService = apiService;
        _logger = logger;
    }

    public async Task<int> SyncInvoicesAsync(Guid userId, bool fullSync = false)
    {
        var connection = await _connectionProvider.GetByUserIdAsync(userId);
        if (connection == null)
            throw new InvalidOperationException("No Fakturoid connection found for user");

        var updatedSince = fullSync ? null : connection.LastSyncedAt;
        var totalSynced = 0;
        var page = 1;

        while (true)
        {
            var invoices = await _apiService.FetchInvoicesAsync(connection.Id, page, updatedSince);

            if (invoices.Count == 0)
                break;

            foreach (var invoice in invoices)
            {
                await _invoiceProvider.CreateOrUpdateAsync(connection.Id, invoice);
                totalSynced++;
            }

            // Fakturoid returns 40 invoices per page
            if (invoices.Count < 40)
                break;

            page++;
        }

        await _connectionProvider.UpdateLastSyncedAsync(connection.Id);

        _logger.LogInformation("Synced {Count} invoices for user {UserId}", totalSynced, userId);

        return totalSynced;
    }

    public async Task<SyncResult> GetSyncStatusAsync(Guid userId)
    {
        var connection = await _connectionProvider.GetByUserIdAsync(userId);
        if (connection == null)
            return new SyncResult { TotalInvoices = 0 };

        var totalCount = await _invoiceProvider.GetTotalCountAsync(connection.Id);

        return new SyncResult
        {
            TotalInvoices = totalCount,
            LastSyncedAt = connection.LastSyncedAt
        };
    }
}
