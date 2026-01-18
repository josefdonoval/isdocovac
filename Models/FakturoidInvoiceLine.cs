namespace Isdocovac.Models;

public class FakturoidInvoiceLine
{
    public Guid Id { get; set; }
    public Guid FakturoidInvoiceId { get; set; }
    public int LineOrder { get; set; } // To maintain order from API

    // Line Item Fields
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public string? UnitName { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; } = 0;

    // Calculated fields (read-only from API)
    public decimal UnitPriceWithoutVat { get; set; }
    public decimal UnitPriceWithVat { get; set; }
    public decimal TotalPriceWithoutVat { get; set; }
    public decimal TotalVat { get; set; }
    public decimal TotalPriceWithVat { get; set; }

    // Inventory tracking
    public int? InventoryItemId { get; set; }
    public string? Sku { get; set; }

    // Navigation properties
    public FakturoidInvoice Invoice { get; set; } = null!;
}
