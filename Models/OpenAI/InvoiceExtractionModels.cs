using Isdocovac.Models.Enums;

namespace Isdocovac.Models.OpenAI;

/// <summary>
/// Request for invoice data extraction from OpenAI.
/// </summary>
public class InvoiceExtractionRequest
{
    public string FileId { get; set; } = string.Empty;
    public InvoiceLineMode LineMode { get; set; }
}

/// <summary>
/// Result of invoice data extraction from OpenAI.
/// </summary>
public class InvoiceExtractionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    // Invoice Header
    public string? InvoiceNumber { get; set; }
    public string? DocumentType { get; set; }
    public DateTime? IssuedOn { get; set; }
    public DateTime? DueOn { get; set; }
    public DateTime? TaxableSupplyDate { get; set; }

    // Parties
    public PartyDetails Supplier { get; set; } = new();
    public PartyDetails Customer { get; set; } = new();

    // Line Items
    public List<InvoiceLineItem> Lines { get; set; } = new();

    // Financial Totals
    public decimal? Subtotal { get; set; }
    public decimal? Total { get; set; }
    public string? Currency { get; set; }
    public string? VatPriceMode { get; set; } // "without_vat" or "from_total_with_vat"
    public List<VatRateSummary> VatRates { get; set; } = new();

    // Payment Information
    public string? VariableSymbol { get; set; }
    public string? ConstantSymbol { get; set; }
    public string? SpecificSymbol { get; set; }
    public string? BankAccount { get; set; }
    public string? Iban { get; set; }
    public string? Note { get; set; }

    // OpenAI Metadata
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public string Model { get; set; } = string.Empty;
}

/// <summary>
/// Supplier or Customer party details.
/// </summary>
public class PartyDetails
{
    public string? Name { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? Zip { get; set; }
    public string? Country { get; set; }
    public string? RegistrationNo { get; set; }
    public string? VatNo { get; set; }
}

/// <summary>
/// Individual invoice line item.
/// </summary>
public class InvoiceLineItem
{
    public int LineNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? UnitName { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal VatRate { get; set; }
    public decimal TotalPriceWithoutVat { get; set; }
    public decimal TotalVat { get; set; }
    public decimal TotalPriceWithVat { get; set; }
    public string? Sku { get; set; }
}

/// <summary>
/// VAT rate summary with base and VAT amount.
/// </summary>
public class VatRateSummary
{
    public decimal VatRate { get; set; }
    public decimal Base { get; set; }
    public decimal Vat { get; set; }
}
