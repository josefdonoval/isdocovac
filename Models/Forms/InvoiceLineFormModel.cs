using System.ComponentModel.DataAnnotations;

namespace Isdocovac.Models.Forms;

public class InvoiceLineFormModel
{
    public Guid? Id { get; set; } // Null for new lines, set for existing lines

    [Required(ErrorMessage = "Item description is required")]
    [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Quantity is required")]
    [Range(0.0001, 999999, ErrorMessage = "Quantity must be greater than 0")]
    public decimal Quantity { get; set; } = 1;

    [MaxLength(50, ErrorMessage = "Unit name cannot exceed 50 characters")]
    public string? UnitName { get; set; }

    [Required(ErrorMessage = "Unit price is required")]
    [Range(0, 999999999, ErrorMessage = "Unit price must be 0 or greater")]
    public decimal UnitPrice { get; set; }

    [Required(ErrorMessage = "VAT rate is required")]
    [Range(0, 100, ErrorMessage = "VAT rate must be between 0 and 100")]
    public decimal VatRate { get; set; } = 21; // Default to Czech standard VAT rate

    [MaxLength(100, ErrorMessage = "SKU cannot exceed 100 characters")]
    public string? Sku { get; set; }

    // Calculated fields (computed client-side)
    public decimal UnitPriceWithoutVat { get; set; }
    public decimal UnitPriceWithVat { get; set; }
    public decimal TotalPriceWithoutVat { get; set; }
    public decimal TotalVat { get; set; }
    public decimal TotalPriceWithVat { get; set; }

    public int LineOrder { get; set; }

    /// <summary>
    /// Recalculates all price fields based on Quantity, UnitPrice, and VatRate
    /// </summary>
    public void RecalculatePrices()
    {
        UnitPriceWithoutVat = UnitPrice;
        UnitPriceWithVat = Math.Round(UnitPrice * (1 + VatRate / 100), 2);
        TotalPriceWithoutVat = Math.Round(Quantity * UnitPriceWithoutVat, 2);
        TotalVat = Math.Round(TotalPriceWithoutVat * (VatRate / 100), 2);
        TotalPriceWithVat = Math.Round(TotalPriceWithoutVat + TotalVat, 2);
    }

    /// <summary>
    /// Converts this form model to an InvoiceLine entity
    /// </summary>
    public InvoiceLine ToEntity(Guid invoiceId)
    {
        return new InvoiceLine
        {
            Id = Id ?? Guid.NewGuid(),
            InvoiceId = invoiceId,
            LineOrder = LineOrder,
            Name = Name,
            Quantity = Quantity,
            UnitName = UnitName,
            UnitPrice = UnitPrice,
            VatRate = VatRate,
            UnitPriceWithoutVat = UnitPriceWithoutVat,
            UnitPriceWithVat = UnitPriceWithVat,
            TotalPriceWithoutVat = TotalPriceWithoutVat,
            TotalVat = TotalVat,
            TotalPriceWithVat = TotalPriceWithVat,
            Sku = Sku
        };
    }

    /// <summary>
    /// Creates a form model from an existing InvoiceLine entity
    /// </summary>
    public static InvoiceLineFormModel FromEntity(InvoiceLine line)
    {
        return new InvoiceLineFormModel
        {
            Id = line.Id,
            Name = line.Name,
            Quantity = line.Quantity,
            UnitName = line.UnitName,
            UnitPrice = line.UnitPrice,
            VatRate = line.VatRate,
            UnitPriceWithoutVat = line.UnitPriceWithoutVat,
            UnitPriceWithVat = line.UnitPriceWithVat,
            TotalPriceWithoutVat = line.TotalPriceWithoutVat,
            TotalVat = line.TotalVat,
            TotalPriceWithVat = line.TotalPriceWithVat,
            Sku = line.Sku,
            LineOrder = line.LineOrder
        };
    }
}
