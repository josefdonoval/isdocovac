using Isdocovac.Models;

namespace Isdocovac.Providers;

public interface IAuthTokenProvider
{
    Task<AuthenticationToken> CreateAsync(string email, string tokenHash, DateTime expiresAt, string? ipAddress, string? userAgent);
    Task<AuthenticationToken?> GetByTokenHashAsync(string tokenHash);
    Task<IEnumerable<AuthenticationToken>> GetUnconsumedByEmailAsync(string email);
    Task<AuthenticationToken> MarkConsumedAsync(Guid tokenId);
    Task RevokeAsync(Guid tokenId);
    Task<int> CleanupExpiredAsync();
}
