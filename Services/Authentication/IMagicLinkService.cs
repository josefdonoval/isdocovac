using Isdocovac.Models;

namespace Isdocovac.Services.Authentication;

public interface IMagicLinkService
{
    /// <summary>
    /// Generates a magic link token and stores it in the database
    /// </summary>
    /// <param name="email">The email address to send the magic link to</param>
    /// <param name="ipAddress">The IP address of the requester (for security)</param>
    /// <param name="userAgent">The user agent of the requester (for security)</param>
    /// <returns>The plain text token to be sent via email</returns>
    Task<string> GenerateTokenAsync(string email, string? ipAddress, string? userAgent);

    /// <summary>
    /// Validates a magic link token
    /// </summary>
    /// <param name="token">The plain text token from the magic link</param>
    /// <returns>The user if the token is valid, null otherwise</returns>
    Task<User?> ValidateTokenAsync(string token);

    /// <summary>
    /// Cleanup expired tokens from the database
    /// </summary>
    /// <returns>Number of tokens cleaned up</returns>
    Task<int> CleanupExpiredTokensAsync();
}
