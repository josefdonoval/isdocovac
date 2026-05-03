using System.Net;
using FluentAssertions;
using Isdocovac.Models;
using Isdocovac.Providers;
using Isdocovac.Services.Fakturoid;
using Isdocovac.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;

namespace Isdocovac.Tests.Services.Fakturoid;

public class FakturoidApiServiceTests
{
    private readonly Mock<IFakturoidConnectionProvider> _connectionProviderMock = new();
    private readonly Mock<IFakturoidOAuthService> _oauthServiceMock = new();
    private readonly Mock<ILogger<FakturoidApiService>> _loggerMock = new();

    private const string AccountsJson = """[{"slug":"test-account","name":"Test"}]""";
    private const string InvoiceJson = """[{"id":1,"number":"INV-001","document_type":"invoice","status":"open","subtotal":1000,"total":1210,"remaining_amount":1210,"currency":"CZK","open":true,"sent":false,"overdue":false,"paid":false,"cancelled":false,"uncollectible":false}]""";

    private FakturoidApiService CreateService(HttpClient httpClient)
    {
        var config = TestHelpers.BuildConfiguration(new Dictionary<string, string?>
        {
            { "Fakturoid:UserAgent", "IsdocovacApp (admin@isdocovac.com)" }
        });

        var factory = TestHelpers.CreateMockHttpClientFactory(httpClient);

        return new FakturoidApiService(
            factory.Object,
            config,
            _loggerMock.Object,
            _connectionProviderMock.Object,
            _oauthServiceMock.Object);
    }

    private FakturoidConnection CreateConnection(
        DateTime? expiresAt = null,
        DateTime? lastSyncedAt = null)
    {
        return new FakturoidConnection
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            AccessToken = "valid-token",
            RefreshToken = "refresh-token",
            AccessTokenExpiresAt = expiresAt ?? DateTime.UtcNow.AddHours(1),
            AccountSlug = "test-account",
            AccountName = "Test",
            LastSyncedAt = lastSyncedAt,
            IsActive = true
        };
    }

    [Fact]
    public async Task GetAccountSlugAsync_ReturnsSlugFromResponse()
    {
        var handler = TestHelpers.CreateMockHttpMessageHandler(
            HttpStatusCode.OK, AccountsJson);
        var httpClient = new HttpClient(handler.Object);
        var service = CreateService(httpClient);

        var slug = await service.GetAccountSlugAsync("valid-token");

        slug.Should().Be("test-account");
    }

    [Fact]
    public async Task GetAccountSlugAsync_ThrowsOnEmptyAccounts()
    {
        var handler = TestHelpers.CreateMockHttpMessageHandler(
            HttpStatusCode.OK, "[]");
        var httpClient = new HttpClient(handler.Object);
        var service = CreateService(httpClient);

        var act = () => service.GetAccountSlugAsync("valid-token");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task FetchInvoicesAsync_ThrowsWhenConnectionNotFound()
    {
        var handler = TestHelpers.CreateMockHttpMessageHandler(
            HttpStatusCode.OK, "[]");
        var httpClient = new HttpClient(handler.Object);
        var service = CreateService(httpClient);

        _connectionProviderMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((FakturoidConnection?)null);

        var act = () => service.FetchInvoicesAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task FetchInvoicesAsync_RefreshesTokenWhenNearExpiry()
    {
        var connection = CreateConnection(expiresAt: DateTime.UtcNow.AddMinutes(2));

        _connectionProviderMock
            .Setup(x => x.GetByIdAsync(connection.Id))
            .ReturnsAsync(connection);

        _oauthServiceMock
            .Setup(x => x.RefreshAccessTokenAsync(connection.RefreshToken))
            .ReturnsAsync(("new-access-token", "new-refresh-token", 7200));

        var handler = TestHelpers.CreateMockHttpMessageHandler(
            HttpStatusCode.OK, InvoiceJson);
        var httpClient = new HttpClient(handler.Object);
        var service = CreateService(httpClient);

        await service.FetchInvoicesAsync(connection.Id);

        _oauthServiceMock.Verify(
            x => x.RefreshAccessTokenAsync(connection.RefreshToken),
            Times.Once);

        _connectionProviderMock.Verify(
            x => x.UpdateTokensAsync(
                connection.Id,
                "new-access-token",
                "new-refresh-token",
                It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public async Task FetchInvoicesAsync_FetchesWithoutRefreshWhenTokenValid()
    {
        var connection = CreateConnection(expiresAt: DateTime.UtcNow.AddHours(1));

        _connectionProviderMock
            .Setup(x => x.GetByIdAsync(connection.Id))
            .ReturnsAsync(connection);

        var handler = TestHelpers.CreateMockHttpMessageHandler(
            HttpStatusCode.OK, InvoiceJson);
        var httpClient = new HttpClient(handler.Object);
        var service = CreateService(httpClient);

        var invoices = await service.FetchInvoicesAsync(connection.Id);

        _oauthServiceMock.Verify(
            x => x.RefreshAccessTokenAsync(It.IsAny<string>()),
            Times.Never);

        invoices.Should().HaveCount(1);
    }

    [Fact]
    public async Task FetchInvoicesAsync_AppendsUpdatedSinceWhenProvided()
    {
        var connection = CreateConnection();
        var updatedSince = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        _connectionProviderMock
            .Setup(x => x.GetByIdAsync(connection.Id))
            .ReturnsAsync(connection);

        HttpRequestMessage? capturedRequest = null;
        var handler = TestHelpers.CreateMockHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    InvoiceJson,
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
        });

        var httpClient = new HttpClient(handler.Object);
        var service = CreateService(httpClient);

        await service.FetchInvoicesAsync(connection.Id, updatedSince: updatedSince);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.ToString().Should().Contain("updated_since=");
    }

    [Fact]
    public async Task FetchInvoiceByIdAsync_ReturnsSingleInvoice()
    {
        var connection = CreateConnection();

        _connectionProviderMock
            .Setup(x => x.GetByIdAsync(connection.Id))
            .ReturnsAsync(connection);

        var singleInvoiceJson = """{"id":1,"number":"INV-001","document_type":"invoice","status":"open","subtotal":1000,"total":1210,"remaining_amount":1210,"currency":"CZK","open":true,"sent":false,"overdue":false,"paid":false,"cancelled":false,"uncollectible":false}""";

        var handler = TestHelpers.CreateMockHttpMessageHandler(
            HttpStatusCode.OK, singleInvoiceJson);
        var httpClient = new HttpClient(handler.Object);
        var service = CreateService(httpClient);

        var invoice = await service.FetchInvoiceByIdAsync(connection.Id, 1);

        invoice.Should().NotBeNull();
    }

    [Fact]
    public async Task FetchInvoiceByIdAsync_ThrowsWhenConnectionNotFound()
    {
        var handler = TestHelpers.CreateMockHttpMessageHandler(
            HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(handler.Object);
        var service = CreateService(httpClient);

        _connectionProviderMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((FakturoidConnection?)null);

        var act = () => service.FetchInvoiceByIdAsync(Guid.NewGuid(), 1);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
