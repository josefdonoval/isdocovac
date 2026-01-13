using Isdocovac.Models;

namespace Isdocovac.Providers;

public interface ILoginAttemptProvider
{
    Task RecordAttemptAsync(string email, string? ipAddress, bool wasSuccessful, string? failureReason = null);
    Task<IEnumerable<LoginAttempt>> GetRecentAttemptsByEmailAsync(string email, int minutes);
    Task<IEnumerable<LoginAttempt>> GetRecentAttemptsByIpAsync(string ipAddress, int minutes);
}
