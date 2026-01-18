namespace Isdocovac.Models;

public class InvoiceLine
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public int LineOrder { get; set; }

    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public string? UnitName { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; } = 0;

    public decimal UnitPriceWithoutVat { get; set; }
    public decimal UnitPriceWithVat { get; set; }
    public decimal TotalPriceWithoutVat { get; set; }
    public decimal TotalVat { get; set; }
    public decimal TotalPriceWithVat { get; set; }

    public string? Sku { get; set; }

    public Invoice Invoice { get; set; } = null!;
}
