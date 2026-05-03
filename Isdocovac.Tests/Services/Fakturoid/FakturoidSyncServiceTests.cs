using FluentAssertions;
using Isdocovac.Models;
using Isdocovac.Providers;
using Isdocovac.Services.Fakturoid;
using Microsoft.Extensions.Logging;
using Moq;

namespace Isdocovac.Tests.Services.Fakturoid;

public class FakturoidSyncServiceTests
{
    private readonly Mock<IFakturoidConnectionProvider> _connectionProviderMock = new();
    private readonly Mock<IFakturoidInvoiceProvider> _invoiceProviderMock = new();
    private readonly Mock<IFakturoidApiService> _apiServiceMock = new();
    private readonly Mock<ILogger<FakturoidSyncService>> _loggerMock = new();

    private FakturoidSyncService CreateService()
    {
        return new FakturoidSyncService(
            _connectionProviderMock.Object,
            _invoiceProviderMock.Object,
            _apiServiceMock.Object,
            _loggerMock.Object);
    }

    private FakturoidConnection CreateConnection(DateTime? lastSyncedAt = null)
    {
        return new FakturoidConnection
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            AccessToken = "valid-token",
            RefreshToken = "refresh-token",
            AccessTokenExpiresAt = DateTime.UtcNow.AddHours(1),
            AccountSlug = "test-account",
            AccountName = "Test",
            LastSyncedAt = lastSyncedAt,
            IsActive = true
        };
    }

    private FakturoidInvoice CreateInvoice(int fakturoidId, Guid connectionId)
    {
        return new FakturoidInvoice
        {
            Id = Guid.NewGuid(),
            FakturoidId = fakturoidId,
            Number = $"INV-{fakturoidId:D3}",
            FakturoidConnectionId = connectionId
        };
    }

    [Fact]
    public async Task SyncInvoicesAsync_ThrowsWhenNoConnection()
    {
        _connectionProviderMock
            .Setup(x => x.GetByCompanyIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((FakturoidConnection?)null);

        var service = CreateService();

        var act = () => service.SyncInvoicesAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SyncInvoicesAsync_SyncsSinglePageOfInvoices()
    {
        var connection = CreateConnection();
        var invoices = new List<FakturoidInvoice>
        {
            CreateInvoice(1, connection.Id),
            CreateInvoice(2, connection.Id)
        };

        _connectionProviderMock
            .Setup(x => x.GetByCompanyIdAsync(connection.CompanyId))
            .ReturnsAsync(connection);

        _apiServiceMock
            .Setup(x => x.FetchInvoicesAsync(
                connection.Id, It.IsAny<int>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(invoices);

        _invoiceProviderMock
            .Setup(x => x.CreateOrUpdateAsync(connection.Id, It.IsAny<FakturoidInvoice>()))
            .ReturnsAsync((Guid connectionId, FakturoidInvoice inv) => inv);

        var service = CreateService();

        var count = await service.SyncInvoicesAsync(connection.CompanyId);

        count.Should().Be(2);
        _invoiceProviderMock.Verify(
            x => x.CreateOrUpdateAsync(connection.Id, It.IsAny<FakturoidInvoice>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task SyncInvoicesAsync_SyncsMultiplePages()
    {
        var connection = CreateConnection();

        // First page: 40 invoices (full page triggers next page fetch)
        var page1 = Enumerable.Range(1, 40)
            .Select(i => CreateInvoice(i, connection.Id))
            .ToList();

        // Second page: 5 invoices (less than 40, stops pagination)
        var page2 = Enumerable.Range(41, 5)
            .Select(i => CreateInvoice(i, connection.Id))
            .ToList();

        _connectionProviderMock
            .Setup(x => x.GetByCompanyIdAsync(connection.CompanyId))
            .ReturnsAsync(connection);

        _apiServiceMock
            .SetupSequence(x => x.FetchInvoicesAsync(
                connection.Id, It.IsAny<int>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(page1)
            .ReturnsAsync(page2);

        _invoiceProviderMock
            .Setup(x => x.CreateOrUpdateAsync(connection.Id, It.IsAny<FakturoidInvoice>()))
            .ReturnsAsync((Guid connectionId, FakturoidInvoice inv) => inv);

        var service = CreateService();

        var count = await service.SyncInvoicesAsync(connection.CompanyId);

        count.Should().Be(45);
    }

    [Fact]
    public async Task SyncInvoicesAsync_UsesIncrementalSyncByDefault()
    {
        var lastSynced = DateTime.UtcNow.AddDays(-1);
        var connection = CreateConnection(lastSyncedAt: lastSynced);

        _connectionProviderMock
            .Setup(x => x.GetByCompanyIdAsync(connection.CompanyId))
            .ReturnsAsync(connection);

        _apiServiceMock
            .Setup(x => x.FetchInvoicesAsync(
                connection.Id, It.IsAny<int>(), lastSynced))
            .ReturnsAsync(new List<FakturoidInvoice>());

        var service = CreateService();

        await service.SyncInvoicesAsync(connection.CompanyId);

        _apiServiceMock.Verify(
            x => x.FetchInvoicesAsync(connection.Id, It.IsAny<int>(), lastSynced),
            Times.Once);
    }

    [Fact]
    public async Task SyncInvoicesAsync_UsesFullSyncWhenRequested()
    {
        var lastSynced = DateTime.UtcNow.AddDays(-1);
        var connection = CreateConnection(lastSyncedAt: lastSynced);

        _connectionProviderMock
            .Setup(x => x.GetByCompanyIdAsync(connection.CompanyId))
            .ReturnsAsync(connection);

        _apiServiceMock
            .Setup(x => x.FetchInvoicesAsync(
                connection.Id, It.IsAny<int>(), null))
            .ReturnsAsync(new List<FakturoidInvoice>());

        var service = CreateService();

        await service.SyncInvoicesAsync(connection.CompanyId, fullSync: true);

        _apiServiceMock.Verify(
            x => x.FetchInvoicesAsync(connection.Id, It.IsAny<int>(), null),
            Times.Once);
    }

    [Fact]
    public async Task SyncInvoicesAsync_CallsUpdateLastSyncedAsync()
    {
        var connection = CreateConnection();

        _connectionProviderMock
            .Setup(x => x.GetByCompanyIdAsync(connection.CompanyId))
            .ReturnsAsync(connection);

        _apiServiceMock
            .Setup(x => x.FetchInvoicesAsync(
                connection.Id, It.IsAny<int>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(new List<FakturoidInvoice>());

        var service = CreateService();

        await service.SyncInvoicesAsync(connection.CompanyId);

        _connectionProviderMock.Verify(
            x => x.UpdateLastSyncedAsync(connection.Id),
            Times.Once);
    }

    [Fact]
    public async Task GetSyncStatusAsync_ReturnsEmptyWhenNoConnection()
    {
        _connectionProviderMock
            .Setup(x => x.GetByCompanyIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((FakturoidConnection?)null);

        var service = CreateService();

        var result = await service.GetSyncStatusAsync(Guid.NewGuid());

        result.TotalInvoices.Should().Be(0);
        result.LastSyncedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetSyncStatusAsync_ReturnsCorrectStatus()
    {
        var lastSynced = DateTime.UtcNow.AddHours(-2);
        var connection = CreateConnection(lastSyncedAt: lastSynced);

        _connectionProviderMock
            .Setup(x => x.GetByCompanyIdAsync(connection.CompanyId))
            .ReturnsAsync(connection);

        _invoiceProviderMock
            .Setup(x => x.GetTotalCountAsync(connection.Id))
            .ReturnsAsync(42);

        var service = CreateService();

        var result = await service.GetSyncStatusAsync(connection.CompanyId);

        result.TotalInvoices.Should().Be(42);
        result.LastSyncedAt.Should().Be(lastSynced);
    }
}
