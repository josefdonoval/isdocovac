using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Isdocovac.Services.Fakturoid;
using Isdocovac.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;

namespace Isdocovac.Tests.Services.Fakturoid;

public class FakturoidOAuthServiceTests
{
    private const string ClientId = "test-client-id";
    private const string ClientSecret = "test-client-secret";
    private const string RedirectUri = "https://localhost/callback";

    private readonly Mock<ILogger<FakturoidOAuthService>> _loggerMock = new();

    private FakturoidOAuthService CreateService(
        HttpClient? httpClient = null)
    {
        var config = TestHelpers.BuildConfiguration(new Dictionary<string, string?>
        {
            { "Fakturoid:ClientId", ClientId },
            { "Fakturoid:ClientSecret", ClientSecret },
            { "Fakturoid:RedirectUri", RedirectUri }
        });

        var factory = httpClient != null
            ? TestHelpers.CreateMockHttpClientFactory(httpClient)
            : TestHelpers.CreateMockHttpClientFactory(new HttpClient());

        return new FakturoidOAuthService(factory.Object, config, _loggerMock.Object);
    }

    [Fact]
    public void GetAuthorizationUrl_ContainsClientId()
    {
        var service = CreateService();

        var url = service.GetAuthorizationUrl("some-state");

        url.Should().Contain($"client_id={Uri.EscapeDataString(ClientId)}");
    }

    [Fact]
    public void GetAuthorizationUrl_ContainsRedirectUri()
    {
        var service = CreateService();

        var url = service.GetAuthorizationUrl("some-state");

        url.Should().Contain($"redirect_uri={Uri.EscapeDataString(RedirectUri)}");
    }

    [Fact]
    public void GetAuthorizationUrl_ContainsStateParameter()
    {
        var service = CreateService();
        var state = "my-unique-state";

        var url = service.GetAuthorizationUrl(state);

        url.Should().Contain($"state={Uri.EscapeDataString(state)}");
    }

    [Fact]
    public void GetAuthorizationUrl_ContainsResponseTypeCode()
    {
        var service = CreateService();

        var url = service.GetAuthorizationUrl("some-state");

        url.Should().Contain("response_type=code");
    }

    [Fact]
    public async Task ExchangeCodeForTokenAsync_SendsBasicAuthHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = TestHelpers.CreateMockHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"access_token":"at","refresh_token":"rt","expires_in":7200}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
        });

        var httpClient = new HttpClient(handler.Object);
        var service = CreateService(httpClient);

        await service.ExchangeCodeForTokenAsync("code123", RedirectUri);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Authorization.Should().NotBeNull();
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Basic");

        var expectedCredentials = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"));
        capturedRequest.Headers.Authorization.Parameter.Should().Be(expectedCredentials);
    }

    [Fact]
    public async Task ExchangeCodeForTokenAsync_ReturnsTokenTupleOnSuccess()
    {
        var handler = TestHelpers.CreateMockHttpMessageHandler(
            HttpStatusCode.OK,
            """{"access_token":"at","refresh_token":"rt","expires_in":7200}""");

        var httpClient = new HttpClient(handler.Object);
        var service = CreateService(httpClient);

        var (accessToken, refreshToken, expiresIn) =
            await service.ExchangeCodeForTokenAsync("code123", RedirectUri);

        accessToken.Should().Be("at");
        refreshToken.Should().Be("rt");
        expiresIn.Should().Be(7200);
    }

    [Fact]
    public async Task ExchangeCodeForTokenAsync_ThrowsOnFailure()
    {
        var handler = TestHelpers.CreateMockHttpMessageHandler(
            HttpStatusCode.BadRequest, "error");

        var httpClient = new HttpClient(handler.Object);
        var service = CreateService(httpClient);

        var act = () => service.ExchangeCodeForTokenAsync("bad-code", RedirectUri);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task RefreshAccessTokenAsync_ReturnsRefreshedTokens()
    {
        var handler = TestHelpers.CreateMockHttpMessageHandler(
            HttpStatusCode.OK,
            """{"access_token":"new-at","refresh_token":"new-rt","expires_in":7200}""");

        var httpClient = new HttpClient(handler.Object);
        var service = CreateService(httpClient);

        var (accessToken, refreshToken, expiresIn) =
            await service.RefreshAccessTokenAsync("old-refresh-token");

        accessToken.Should().Be("new-at");
        refreshToken.Should().Be("new-rt");
        expiresIn.Should().Be(7200);
    }

    [Fact]
    public async Task RevokeTokenAsync_SendsPostRequest()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = TestHelpers.CreateMockHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var httpClient = new HttpClient(handler.Object);
        var service = CreateService(httpClient);

        await service.RevokeTokenAsync("access-token", "refresh-token");

        capturedRequest.Should().NotBeNull();
        capturedRequest!.Method.Should().Be(HttpMethod.Post);
        capturedRequest.RequestUri!.ToString()
            .Should().Contain("oauth/revoke");
    }
}
