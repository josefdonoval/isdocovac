using Isdocovac.Data;
using Isdocovac.Models;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers;

public interface ILoginAttemptProvider
{
    Task RecordAttemptAsync(string email, string? ipAddress, bool wasSuccessful, string? failureReason = null);
    Task<IEnumerable<LoginAttempt>> GetRecentAttemptsByEmailAsync(string email, int minutes);
    Task<IEnumerable<LoginAttempt>> GetRecentAttemptsByIpAsync(string ipAddress, int minutes);
}

public class LoginAttemptProvider : ILoginAttemptProvider
{
    private readonly ApplicationDbContext _context;

    public LoginAttemptProvider(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task RecordAttemptAsync(string email, string? ipAddress, bool wasSuccessful, string? failureReason = null)
    {
        var attempt = new LoginAttempt
        {
            Id = Guid.NewGuid(),
            Email = email,
            IpAddress = ipAddress,
            AttemptedAt = DateTime.UtcNow,
            WasSuccessful = wasSuccessful,
            FailureReason = failureReason
        };

        _context.LoginAttempts.Add(attempt);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<LoginAttempt>> GetRecentAttemptsByEmailAsync(string email, int minutes)
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-minutes);

        return await _context.LoginAttempts
            .Where(a => a.Email == email && a.AttemptedAt >= cutoffTime)
            .OrderByDescending(a => a.AttemptedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<LoginAttempt>> GetRecentAttemptsByIpAsync(string ipAddress, int minutes)
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-minutes);

        return await _context.LoginAttempts
            .Where(a => a.IpAddress == ipAddress && a.AttemptedAt >= cutoffTime)
            .OrderByDescending(a => a.AttemptedAt)
            .ToListAsync();
    }
}
