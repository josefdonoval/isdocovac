namespace Isdocovac.Models.ISDOC;

/// <summary>
/// ISDOC document structure for XML generation.
/// Based on ISDOC 6.0.2 schema (http://isdoc.cz/6.0.2/xsd/isdoc-invoice-6.0.2.xsd)
/// </summary>
public class IsdocDocument
{
    public string DocumentType { get; set; } = "1"; // 1=invoice, 2=credit_note, 3=debit_note, etc.
    public string Id { get; set; } = string.Empty; // Invoice number
    public Guid Uuid { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime? TaxPointDate { get; set; } // DUZP - datum uskutečnění zdanitelného plnění
    public DateTime? DueDate { get; set; }

    public IsdocParty AccountingSupplierParty { get; set; } = new();
    public IsdocParty AccountingCustomerParty { get; set; } = new();

    public List<IsdocInvoiceLine> InvoiceLines { get; set; } = new();
    public IsdocTaxTotal TaxTotal { get; set; } = new();
    public IsdocLegalMonetaryTotal LegalMonetaryTotal { get; set; } = new();
    public IsdocPaymentMeans? PaymentMeans { get; set; }

    public string? Note { get; set; }
    public string LocalCurrencyCode { get; set; } = "CZK";
}

/// <summary>
/// Party information (Supplier or Customer).
/// </summary>
public class IsdocParty
{
    public IsdocPartyInfo Party { get; set; } = new();
}

public class IsdocPartyInfo
{
    public IsdocPartyIdentification PartyIdentification { get; set; } = new();
    public string? PartyName { get; set; }
    public IsdocPostalAddress? PostalAddress { get; set; }
}

public class IsdocPartyIdentification
{
    public IsdocID? ID { get; set; } // Company registration number
    public IsdocID? CompanyID { get; set; } // VAT number
}

public class IsdocID
{
    public string Value { get; set; } = string.Empty;
    public string? SchemeID { get; set; } // "ICO" for registration, "VAT" for VAT number
}

public class IsdocPostalAddress
{
    public string? StreetName { get; set; }
    public string? CityName { get; set; }
    public string? PostalZone { get; set; } // ZIP code
    public IsdocCountry? Country { get; set; }
}

public class IsdocCountry
{
    public string IdentificationCode { get; set; } = "CZ"; // ISO 3166-1 alpha-2
}

/// <summary>
/// Invoice line item.
/// </summary>
public class IsdocInvoiceLine
{
    public int ID { get; set; } // Line number
    public decimal? InvoicedQuantity { get; set; }
    public string? UnitCode { get; set; } // e.g., "ks", "kg", "MTR"
    public IsdocLineExtensionAmount LineExtensionAmount { get; set; } = new();
    public IsdocLineExtensionAmountTaxInclusive LineExtensionAmountTaxInclusive { get; set; } = new();
    public decimal? UnitPrice { get; set; }
    public decimal? UnitPriceTaxInclusive { get; set; }
    public IsdocClassifiedTaxCategory ClassifiedTaxCategory { get; set; } = new();
    public IsdocItem Item { get; set; } = new();
}

public class IsdocLineExtensionAmount
{
    public decimal Value { get; set; } // Total without VAT
}

public class IsdocLineExtensionAmountTaxInclusive
{
    public decimal Value { get; set; } // Total with VAT
}

public class IsdocClassifiedTaxCategory
{
    public decimal Percent { get; set; } // VAT rate percentage
    public string VATCalculationMethod { get; set; } = "0"; // 0=from_base, 1=from_gross
}

public class IsdocItem
{
    public string? Description { get; set; }
    public string? SellersItemIdentification { get; set; } // SKU
}

/// <summary>
/// Tax total with VAT breakdown per rate.
/// </summary>
public class IsdocTaxTotal
{
    public decimal TaxAmount { get; set; } // Total VAT
    public List<IsdocTaxSubTotal> TaxSubTotal { get; set; } = new();
}

public class IsdocTaxSubTotal
{
    public decimal TaxableAmount { get; set; } // Base amount for this VAT rate
    public decimal TaxAmount { get; set; } // VAT amount for this rate
    public IsdocTaxCategory TaxCategory { get; set; } = new();
}

public class IsdocTaxCategory
{
    public decimal Percent { get; set; } // VAT rate percentage
}

/// <summary>
/// Legal monetary totals.
/// </summary>
public class IsdocLegalMonetaryTotal
{
    public decimal TaxExclusiveAmount { get; set; } // Total without VAT
    public decimal TaxInclusiveAmount { get; set; } // Total with VAT
    public decimal AlreadyClaimedTaxExclusiveAmount { get; set; } = 0m; // For advance payments
    public decimal AlreadyClaimedTaxInclusiveAmount { get; set; } = 0m;
    public decimal DifferenceTaxExclusiveAmount { get; set; } // = TaxExclusiveAmount - AlreadyClaimedTaxExclusiveAmount
    public decimal DifferenceTaxInclusiveAmount { get; set; } // = TaxInclusiveAmount - AlreadyClaimedTaxInclusiveAmount
    public decimal? PayableRoundingAmount { get; set; } = 0m; // Rounding difference
    public decimal PaidDepositsAmount { get; set; } = 0m; // Deposits paid
    public decimal PayableAmount { get; set; } // Amount to pay
}

/// <summary>
/// Payment means (bank account, payment symbols).
/// </summary>
public class IsdocPaymentMeans
{
    public IsdocPayment? Payment { get; set; }
}

public class IsdocPayment
{
    public string? PaidDepositsID { get; set; } // Variable symbol (VS)
    public string? BankCode { get; set; }
    public string? ID { get; set; } // Bank account number
    public string? IBAN { get; set; }
}
