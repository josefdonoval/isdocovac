using Isdocovac.Models;
using Isdocovac.Providers;
using Isdocovac.Utils;

namespace Isdocovac.Services.Authentication;

public class SessionService : ISessionService
{
    private readonly ISessionProvider _sessionProvider;
    private readonly IUserProvider _userProvider;
    private readonly IConfiguration _configuration;

    public SessionService(
        ISessionProvider sessionProvider,
        IUserProvider userProvider,
        IConfiguration configuration)
    {
        _sessionProvider = sessionProvider;
        _userProvider = userProvider;
        _configuration = configuration;
    }

    public async Task<string> CreateSessionAsync(Guid userId, string? ipAddress, string? userAgent)
    {
        // Generate a cryptographically secure random session token
        var plainToken = SecureTokenGenerator.GenerateToken(32);

        // Hash the token before storing
        var tokenHash = TokenHasher.HashToken(plainToken);

        // Get session expiration from configuration (default: 14 days)
        var expirationDays = _configuration.GetValue<int>("Authentication:Session:AbsoluteExpirationDays", 14);
        var expiresAt = DateTime.UtcNow.AddDays(expirationDays);

        // Create the session in the database
        await _sessionProvider.CreateAsync(userId, tokenHash, expiresAt, ipAddress, userAgent);

        // Update user's last login timestamp
        await _userProvider.UpdateLastLoginAsync(userId);

        // Return the plain text token to be stored in a cookie
        return plainToken;
    }

    public async Task<User?> ValidateSessionAsync(string sessionToken)
    {
        // Hash the incoming token
        var tokenHash = TokenHasher.HashToken(sessionToken);

        // Retrieve the session from the database
        var session = await _sessionProvider.GetBySessionTokenHashAsync(tokenHash);

        // Check if session exists
        if (session == null)
            return null;

        // Check if session has expired
        if (session.ExpiresAt < DateTime.UtcNow)
        {
            // Revoke expired session
            await _sessionProvider.RevokeAsync(session.Id);
            return null;
        }

        // Check if session is active
        if (!session.IsActive)
            return null;

        // Update last activity timestamp (sliding expiration)
        await _sessionProvider.UpdateLastActivityAsync(session.Id);

        // Return the user associated with the session
        return session.User;
    }

    public async Task RevokeSessionAsync(string sessionToken)
    {
        // Hash the incoming token
        var tokenHash = TokenHasher.HashToken(sessionToken);

        // Retrieve the session from the database
        var session = await _sessionProvider.GetBySessionTokenHashAsync(tokenHash);

        if (session != null)
        {
            await _sessionProvider.RevokeAsync(session.Id);
        }
    }

    public async Task RevokeAllUserSessionsAsync(Guid userId)
    {
        await _sessionProvider.RevokeAllByUserAsync(userId);
    }

    public async Task<int> CleanupExpiredSessionsAsync()
    {
        return await _sessionProvider.CleanupExpiredAsync();
    }
}
