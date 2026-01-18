namespace Isdocovac.Models;

public class FakturoidConnection
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    // OAuth credentials
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }

    // Fakturoid account information
    public string AccountSlug { get; set; } = string.Empty;
    public string? AccountName { get; set; }

    // Connection metadata
    public DateTime ConnectedAt { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Audit fields
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public ICollection<FakturoidInvoice> Invoices { get; set; } = new List<FakturoidInvoice>();
}
