using Isdocovac.Models;

namespace Isdocovac.Services.Authentication;

public interface ISessionService
{
    /// <summary>
    /// Creates a new session for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="ipAddress">The IP address of the user</param>
    /// <param name="userAgent">The user agent of the user</param>
    /// <returns>The plain text session token to be stored in a cookie</returns>
    Task<string> CreateSessionAsync(Guid userId, string? ipAddress, string? userAgent);

    /// <summary>
    /// Validates a session token and returns the associated user
    /// </summary>
    /// <param name="sessionToken">The plain text session token from the cookie</param>
    /// <returns>The user if the session is valid, null otherwise</returns>
    Task<User?> ValidateSessionAsync(string sessionToken);

    /// <summary>
    /// Revokes a session
    /// </summary>
    /// <param name="sessionToken">The plain text session token to revoke</param>
    Task RevokeSessionAsync(string sessionToken);

    /// <summary>
    /// Revokes all sessions for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    Task RevokeAllUserSessionsAsync(Guid userId);

    /// <summary>
    /// Cleanup expired sessions from the database
    /// </summary>
    /// <returns>Number of sessions cleaned up</returns>
    Task<int> CleanupExpiredSessionsAsync();
}
