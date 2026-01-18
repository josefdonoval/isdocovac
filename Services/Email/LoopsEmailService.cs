using System.Text;
using System.Text.Json;

namespace Isdocovac.Services.Email;

public interface IEmailService
{
    /// <summary>
    /// Sends a magic link email to the specified email address
    /// </summary>
    /// <param name="email">The recipient email address</param>
    /// <param name="magicLinkUrl">The magic link URL</param>
    Task SendMagicLinkEmailAsync(string email, string magicLinkUrl);
}

public class LoopsEmailService : IEmailService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LoopsEmailService> _logger;

    public LoopsEmailService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<LoopsEmailService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendMagicLinkEmailAsync(string email, string magicLinkUrl)
    {
        try
        {
            var apiKey = _configuration["Email:Loops:ApiKey"];
            var transactionalId = _configuration["Email:Loops:TransactionalId"];

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogError("Loops API key is not configured");
                throw new InvalidOperationException("Loops API key is not configured");
            }

            if (string.IsNullOrEmpty(transactionalId))
            {
                _logger.LogWarning("Loops transactional ID is not configured, using placeholder");
                transactionalId = "placeholder"; // Will be updated later
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var payload = new
            {
                transactionalId = transactionalId,
                email = email,
                dataVariables = new
                {
                    magicLinkUrl = magicLinkUrl,
                    email = email
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("https://app.loops.so/api/v1/transactional", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to send email via Loops: {StatusCode} - {Error}",
                    response.StatusCode, errorContent);
                throw new HttpRequestException($"Failed to send email: {response.StatusCode}");
            }

            _logger.LogInformation("Magic link email sent to {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending magic link email to {Email}", email);
            throw;
        }
    }
}
