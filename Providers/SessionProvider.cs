using Isdocovac.Data;
using Isdocovac.Models;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers;

public class SessionProvider : ISessionProvider
{
    private readonly ApplicationDbContext _context;

    public SessionProvider(ApplicationDbContext context)
    {
        _context = context;
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

        _context.UserSessions.Add(session);
        await _context.SaveChangesAsync();

        return session;
    }

    public async Task<UserSession?> GetBySessionTokenHashAsync(string sessionTokenHash)
    {
        return await _context.UserSessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.SessionTokenHash == sessionTokenHash && s.IsActive);
    }

    public async Task<IEnumerable<UserSession>> GetActiveSessionsByUserAsync(Guid userId)
    {
        return await _context.UserSessions
            .Where(s => s.UserId == userId && s.IsActive && s.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(s => s.LastActivityAt)
            .ToListAsync();
    }

    public async Task<UserSession> UpdateLastActivityAsync(Guid sessionId)
    {
        var session = await _context.UserSessions.FindAsync(sessionId);
        if (session == null)
        {
            throw new InvalidOperationException($"Session with ID {sessionId} not found");
        }

        session.LastActivityAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return session;
    }

    public async Task RevokeAsync(Guid sessionId)
    {
        var session = await _context.UserSessions.FindAsync(sessionId);
        if (session != null)
        {
            session.IsActive = false;
            await _context.SaveChangesAsync();
        }
    }

    public async Task RevokeAllByUserAsync(Guid userId)
    {
        var sessions = await _context.UserSessions
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync();

        foreach (var session in sessions)
        {
            session.IsActive = false;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<int> CleanupExpiredAsync()
    {
        var expiredSessions = await _context.UserSessions
            .Where(s => s.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();

        foreach (var session in expiredSessions)
        {
            session.IsActive = false;
        }

        return await _context.SaveChangesAsync();
    }
}
