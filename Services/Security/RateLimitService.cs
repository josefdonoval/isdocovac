using Isdocovac.Providers;

namespace Isdocovac.Services.Security;

public class RateLimitService : IRateLimitService
{
    private readonly ILoginAttemptProvider _loginAttemptProvider;
    private readonly IConfiguration _configuration;

    public RateLimitService(
        ILoginAttemptProvider loginAttemptProvider,
        IConfiguration configuration)
    {
        _loginAttemptProvider = loginAttemptProvider;
        _configuration = configuration;
    }

    public async Task<bool> IsEmailAllowedAsync(string email)
    {
        // Get configuration values (defaults: 3 attempts per 15 minutes)
        var perEmailLimit = _configuration.GetValue<int>("RateLimiting:MagicLink:PerEmailLimit", 3);
        var windowMinutes = _configuration.GetValue<int>("RateLimiting:MagicLink:WindowMinutes", 15);

        // Get recent attempts for this email
        var recentAttempts = await _loginAttemptProvider.GetRecentAttemptsByEmailAsync(email, windowMinutes);

        // Count attempts in the time window
        var attemptCount = recentAttempts.Count();

        // Check if limit exceeded
        return attemptCount < perEmailLimit;
    }

    public async Task<bool> IsIpAddressAllowedAsync(string ipAddress)
    {
        // Get configuration values (defaults: 10 attempts per 15 minutes)
        var perIpLimit = _configuration.GetValue<int>("RateLimiting:MagicLink:PerIpLimit", 10);
        var windowMinutes = _configuration.GetValue<int>("RateLimiting:MagicLink:WindowMinutes", 15);

        // Get recent attempts for this IP
        var recentAttempts = await _loginAttemptProvider.GetRecentAttemptsByIpAsync(ipAddress, windowMinutes);

        // Count attempts in the time window
        var attemptCount = recentAttempts.Count();

        // Check if limit exceeded
        return attemptCount < perIpLimit;
    }

    public async Task RecordAttemptAsync(string email, string? ipAddress, bool wasSuccessful, string? failureReason = null)
    {
        await _loginAttemptProvider.RecordAttemptAsync(email, ipAddress, wasSuccessful, failureReason);
    }
}
