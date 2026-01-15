using Isdocovac.Data;
using Isdocovac.Models;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers;

public class SessionProvider : ISessionProvider
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public SessionProvider(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<UserSession> CreateAsync(Guid userId, string sessionTokenHash, DateTime expiresAt, string? ipAddress, string? userAgent)
    {
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionTokenHash = sessionTokenHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            LastActivityAt = DateTime.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            IsActive = true
        };

        await using var context = _contextFactory.CreateDbContext();
        context.UserSessions.Add(session);
        await context.SaveChangesAsync();

        return session;
    }

    public async Task<UserSession?> GetBySessionTokenHashAsync(string sessionTokenHash)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.UserSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.SessionTokenHash == sessionTokenHash && s.IsActive);
    }

    public async Task<IEnumerable<UserSession>> GetActiveSessionsByUserAsync(Guid userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.UserSessions
            .Where(s => s.UserId == userId && s.IsActive && s.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(s => s.LastActivityAt)
            .ToListAsync();
    }

    public async Task<UserSession> UpdateLastActivityAsync(Guid sessionId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var session = await context.UserSessions.FindAsync(sessionId);
        if (session == null)
        {
            throw new InvalidOperationException($"Session with ID {sessionId} not found");
        }

        session.LastActivityAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return session;
    }

    public async Task RevokeAsync(Guid sessionId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var session = await context.UserSessions.FindAsync(sessionId);
        if (session != null)
        {
            session.IsActive = false;
            await context.SaveChangesAsync();
        }
    }

    public async Task RevokeAllByUserAsync(Guid userId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var sessions = await context.UserSessions
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync();

        foreach (var session in sessions)
        {
            session.IsActive = false;
        }

        await context.SaveChangesAsync();
    }

    public async Task<int> CleanupExpiredAsync()
    {
        await using var context = _contextFactory.CreateDbContext();
        var expiredSessions = await context.UserSessions
            .Where(s => s.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();

        foreach (var session in expiredSessions)
        {
            session.IsActive = false;
        }

        return await context.SaveChangesAsync();
    }
}
