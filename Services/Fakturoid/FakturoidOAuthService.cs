using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Isdocovac.Services.Fakturoid;

public interface IFakturoidOAuthService
{
    string GetAuthorizationUrl(string state);
    Task<(string accessToken, string refreshToken, int expiresIn)> ExchangeCodeForTokenAsync(string code, string redirectUri);
    Task<(string accessToken, string refreshToken, int expiresIn)> RefreshAccessTokenAsync(string refreshToken);
    Task RevokeTokenAsync(string accessToken, string refreshToken);
}

public class FakturoidOAuthService : IFakturoidOAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FakturoidOAuthService> _logger;

    public FakturoidOAuthService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<FakturoidOAuthService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public string GetAuthorizationUrl(string state)
    {
        var clientId = _configuration["Fakturoid:ClientId"];
        var redirectUri = _configuration["Fakturoid:RedirectUri"];

        var queryParams = new Dictionary<string, string>
        {
            { "client_id", clientId! },
            { "redirect_uri", redirectUri! },
            { "response_type", "code" },
            { "state", state }
        };

        var queryString = string.Join("&", queryParams.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        return $"https://app.fakturoid.cz/api/v3/oauth?{queryString}";
    }

    public async Task<(string accessToken, string refreshToken, int expiresIn)> ExchangeCodeForTokenAsync(
        string code,
        string redirectUri)
    {
        var clientId = _configuration["Fakturoid:ClientId"];
        var clientSecret = _configuration["Fakturoid:ClientSecret"];

        var httpClient = _httpClientFactory.CreateClient();

        // Add Basic Auth header
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Isdocovac/1.0");

        var formData = new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", code },
            { "redirect_uri", redirectUri }
        };

        var content = new FormUrlEncodedContent(formData);
        var response = await httpClient.PostAsync("https://app.fakturoid.cz/api/v3/oauth/token", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to exchange code for token: {StatusCode} - {Error}",
                response.StatusCode, errorContent);
            throw new HttpRequestException($"Failed to exchange code for token: {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<FakturoidTokenResponse>(json);

        if (tokenResponse == null)
            throw new InvalidOperationException("Invalid token response");

        return (tokenResponse.access_token, tokenResponse.refresh_token, tokenResponse.expires_in);
    }

    public async Task<(string accessToken, string refreshToken, int expiresIn)> RefreshAccessTokenAsync(
        string refreshToken)
    {
        var clientId = _configuration["Fakturoid:ClientId"];
        var clientSecret = _configuration["Fakturoid:ClientSecret"];

        var httpClient = _httpClientFactory.CreateClient();

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Isdocovac/1.0");

        var formData = new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken }
        };

        var content = new FormUrlEncodedContent(formData);
        var response = await httpClient.PostAsync("https://app.fakturoid.cz/api/v3/oauth/token", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to refresh token: {StatusCode} - {Error}",
                response.StatusCode, errorContent);
            throw new HttpRequestException($"Failed to refresh token: {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<FakturoidTokenResponse>(json);

        if (tokenResponse == null)
            throw new InvalidOperationException("Invalid token response");

        return (tokenResponse.access_token, tokenResponse.refresh_token, tokenResponse.expires_in);
    }

    public async Task RevokeTokenAsync(string accessToken, string refreshToken)
    {
        var clientId = _configuration["Fakturoid:ClientId"];
        var clientSecret = _configuration["Fakturoid:ClientSecret"];

        var httpClient = _httpClientFactory.CreateClient();

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Isdocovac/1.0");

        await httpClient.PostAsync("https://app.fakturoid.cz/api/v3/oauth/revoke", null);
    }

    private class FakturoidTokenResponse
    {
        public string access_token { get; set; } = string.Empty;
        public string refresh_token { get; set; } = string.Empty;
        public int expires_in { get; set; }
    }
}
