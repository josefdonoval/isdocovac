using Isdocovac.Data;
using Isdocovac.Models;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers;

public class AuthTokenProvider : IAuthTokenProvider
{
    private readonly ApplicationDbContext _context;

    public AuthTokenProvider(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AuthenticationToken> CreateAsync(string email, string tokenHash, DateTime expiresAt, string? ipAddress, string? userAgent)
    {
        var token = new AuthenticationToken
        {
            Id = Guid.NewGuid(),
            Email = email,
            TokenHash = tokenHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            IsRevoked = false
        };

        _context.AuthenticationTokens.Add(token);
        await _context.SaveChangesAsync();

        return token;
    }

    public async Task<AuthenticationToken?> GetByTokenHashAsync(string tokenHash)
    {
        return await _context.AuthenticationTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
    }

    public async Task<IEnumerable<AuthenticationToken>> GetUnconsumedByEmailAsync(string email)
    {
        return await _context.AuthenticationTokens
            .Where(t => t.Email == email && t.ConsumedAt == null && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();
    }

    public async Task<AuthenticationToken> MarkConsumedAsync(Guid tokenId)
    {
        var token = await _context.AuthenticationTokens.FindAsync(tokenId);
        if (token == null)
        {
            throw new InvalidOperationException($"Token with ID {tokenId} not found");
        }

        token.ConsumedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return token;
    }

    public async Task RevokeAsync(Guid tokenId)
    {
        var token = await _context.AuthenticationTokens.FindAsync(tokenId);
        if (token != null)
        {
            token.IsRevoked = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> CleanupExpiredAsync()
    {
        var expiredTokens = await _context.AuthenticationTokens
            .Where(t => t.ExpiresAt < DateTime.UtcNow || t.ConsumedAt != null)
            .ToListAsync();

        _context.AuthenticationTokens.RemoveRange(expiredTokens);
        return await _context.SaveChangesAsync();
    }
}
