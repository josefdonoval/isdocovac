namespace Isdocovac.Models.Enums;

/// <summary>
/// Defines how invoice line items should be extracted from PDF invoices.
/// </summary>
public enum InvoiceLineMode
{
    /// <summary>
    /// Extract individual line items with detailed information (quantity, unit price, VAT per item).
    /// </summary>
    Detailed = 10,

    /// <summary>
    /// Create summary line item(s) with overall totals.
    /// </summary>
    Overall = 20
}
