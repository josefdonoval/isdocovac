using Isdocovac.Models;

namespace Isdocovac.Providers;

public interface ISessionProvider
{
    Task<UserSession> CreateAsync(Guid userId, string sessionTokenHash, DateTime expiresAt, string? ipAddress, string? userAgent);
    Task<UserSession?> GetBySessionTokenHashAsync(string sessionTokenHash);
    Task<IEnumerable<UserSession>> GetActiveSessionsByUserAsync(Guid userId);
    Task<UserSession> UpdateLastActivityAsync(Guid sessionId);
    Task RevokeAsync(Guid sessionId);
    Task RevokeAllByUserAsync(Guid userId);
    Task<int> CleanupExpiredAsync();
}
