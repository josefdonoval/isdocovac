using System.Net;
using FluentAssertions;
using Isdocovac.Services.Email;
using Isdocovac.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace Isdocovac.Tests.Services.Email;

public class LoopsEmailServiceTests
{
    private readonly Mock<ILogger<LoopsEmailService>> _loggerMock;

    public LoopsEmailServiceTests()
    {
        _loggerMock = new Mock<ILogger<LoopsEmailService>>();
    }

    private LoopsEmailService CreateService(
        Dictionary<string, string?> configSettings,
        Mock<HttpMessageHandler>? handlerMock = null)
    {
        var config = TestHelpers.BuildConfiguration(configSettings);
        var handler = handlerMock ?? TestHelpers.CreateMockHttpMessageHandler(HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(handler.Object);
        var factory = TestHelpers.CreateMockHttpClientFactory(httpClient);
        return new LoopsEmailService(factory.Object, config, _loggerMock.Object);
    }

    [Fact]
    public async Task SendMagicLinkEmailAsync_ThrowsInvalidOperationException_WhenApiKeyMissing()
    {
        var service = CreateService(new Dictionary<string, string?>());

        var act = () => service.SendMagicLinkEmailAsync("test@example.com", "https://example.com/magic-link");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SendMagicLinkEmailAsync_SendsCorrectPostRequest_WithAuthHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        var handlerMock = TestHelpers.CreateMockHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}")
            };
        });

        var service = CreateService(new Dictionary<string, string?>
        {
            { "Email:Loops:ApiKey", "test-api-key" },
            { "Email:Loops:TransactionalId", "txn-123" }
        }, handlerMock);

        await service.SendMagicLinkEmailAsync("test@example.com", "https://example.com/magic-link");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.RequestUri!.ToString().Should().Be("https://app.loops.so/api/v1/transactional");
        capturedRequest.Headers.Authorization.Should().NotBeNull();
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        capturedRequest.Headers.Authorization!.Parameter.Should().Be("test-api-key");
    }

    [Fact]
    public async Task SendMagicLinkEmailAsync_UsesPlaceholderTransactionalId_WhenNotConfigured()
    {
        HttpRequestMessage? capturedRequest = null;
        var handlerMock = TestHelpers.CreateMockHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}")
            };
        });

        var service = CreateService(new Dictionary<string, string?>
        {
            { "Email:Loops:ApiKey", "test-api-key" }
        }, handlerMock);

        await service.SendMagicLinkEmailAsync("test@example.com", "https://example.com/magic-link");

        capturedRequest.Should().NotBeNull();
        var body = await capturedRequest!.Content!.ReadAsStringAsync();
        body.Should().Contain("placeholder");
    }

    [Fact]
    public async Task SendMagicLinkEmailAsync_ThrowsHttpRequestException_OnFailureResponse()
    {
        var handlerMock = TestHelpers.CreateMockHttpMessageHandler(
            HttpStatusCode.InternalServerError,
            "{\"error\": \"server error\"}");

        var service = CreateService(new Dictionary<string, string?>
        {
            { "Email:Loops:ApiKey", "test-api-key" }
        }, handlerMock);

        var act = () => service.SendMagicLinkEmailAsync("test@example.com", "https://example.com/magic-link");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task SendMagicLinkEmailAsync_Succeeds_On200Response()
    {
        var handlerMock = TestHelpers.CreateMockHttpMessageHandler(
            HttpStatusCode.OK,
            "{\"success\": true}");

        var service = CreateService(new Dictionary<string, string?>
        {
            { "Email:Loops:ApiKey", "test-api-key" },
            { "Email:Loops:TransactionalId", "txn-123" }
        }, handlerMock);

        var act = () => service.SendMagicLinkEmailAsync("test@example.com", "https://example.com/magic-link");

        await act.Should().NotThrowAsync();
    }
}
