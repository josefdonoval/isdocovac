namespace Isdocovac.Models;

public class FakturoidOAuthState
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string StateHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
