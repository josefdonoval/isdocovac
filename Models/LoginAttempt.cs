namespace Isdocovac.Models;

public class LoginAttempt
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public DateTime AttemptedAt { get; set; }
    public bool WasSuccessful { get; set; }
    public string? FailureReason { get; set; }
}
