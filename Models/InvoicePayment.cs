namespace Isdocovac.Models;

public class InvoicePayment
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }

    public DateTime PaidOn { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? VariableSymbol { get; set; }

    public DateTime CreatedAt { get; set; }

    public Invoice Invoice { get; set; } = null!;
}
