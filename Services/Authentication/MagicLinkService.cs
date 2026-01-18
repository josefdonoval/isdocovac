using Isdocovac.Models;
using Isdocovac.Providers;
using Isdocovac.Utils;

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

public class MagicLinkService : IMagicLinkService
{
    private readonly IAuthTokenProvider _authTokenProvider;
    private readonly IUserProvider _userProvider;
    private readonly IConfiguration _configuration;

    public MagicLinkService(
        IAuthTokenProvider authTokenProvider,
        IUserProvider userProvider,
        IConfiguration configuration)
    {
        _authTokenProvider = authTokenProvider;
        _userProvider = userProvider;
        _configuration = configuration;
    }

    public async Task<string> GenerateTokenAsync(string email, string? ipAddress, string? userAgent)
    {
        // Generate a cryptographically secure random token
        var plainToken = SecureTokenGenerator.GenerateToken(32);

        // Hash the token before storing
        var tokenHash = TokenHasher.HashToken(plainToken);

        // Get token expiration from configuration (default: 15 minutes)
        var expirationMinutes = _configuration.GetValue<int>("Authentication:MagicLink:ExpirationMinutes", 15);
        var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

        // Store the hashed token in the database
        await _authTokenProvider.CreateAsync(email, tokenHash, expiresAt, ipAddress, userAgent);

        // Return the plain text token to be sent via email
        return plainToken;
    }

    public async Task<User?> ValidateTokenAsync(string token)
    {
        // Hash the incoming token
        var tokenHash = TokenHasher.HashToken(token);

        // Retrieve the token from the database
        var authToken = await _authTokenProvider.GetByTokenHashAsync(tokenHash);

        // Check if token exists
        if (authToken == null)
            return null;

        // Check if token has expired
        if (authToken.ExpiresAt < DateTime.UtcNow)
            return null;

        // Check if token has already been consumed
        if (authToken.ConsumedAt != null)
            return null;

        // Check if token has been revoked
        if (authToken.IsRevoked)
            return null;

        // Mark token as consumed (single-use)
        await _authTokenProvider.MarkConsumedAsync(authToken.Id);

        // Get or create user by email
        var user = await _userProvider.GetOrCreateByEmailAsync(authToken.Email);

        // Mark email as verified since they clicked the magic link
        if (!user.EmailVerified)
        {
            await _userProvider.UpdateEmailVerificationAsync(user.Id, true);
        }

        return user;
    }

    public async Task<int> CleanupExpiredTokensAsync()
    {
        return await _authTokenProvider.CleanupExpiredAsync();
    }
}
