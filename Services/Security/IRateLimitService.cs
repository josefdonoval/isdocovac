namespace Isdocovac.Services.Security;

public interface IRateLimitService
{
    /// <summary>
    /// Checks if a magic link request is allowed for the given email
    /// </summary>
    /// <param name="email">The email address</param>
    /// <returns>True if allowed, false if rate limit exceeded</returns>
    Task<bool> IsEmailAllowedAsync(string email);

    /// <summary>
    /// Checks if a magic link request is allowed for the given IP address
    /// </summary>
    /// <param name="ipAddress">The IP address</param>
    /// <returns>True if allowed, false if rate limit exceeded</returns>
    Task<bool> IsIpAddressAllowedAsync(string ipAddress);

    /// <summary>
    /// Records a magic link attempt for rate limiting
    /// </summary>
    /// <param name="email">The email address</param>
    /// <param name="ipAddress">The IP address</param>
    /// <param name="wasSuccessful">Whether the attempt was successful</param>
    /// <param name="failureReason">The reason for failure (if applicable)</param>
    Task RecordAttemptAsync(string email, string? ipAddress, bool wasSuccessful, string? failureReason = null);
}
