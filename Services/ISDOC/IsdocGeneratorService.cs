using System.Globalization;
using System.Xml.Linq;
using Isdocovac.Models.Extraction;

namespace Isdocovac.Services.ISDOC;

public interface IIsdocGeneratorService
{
    Task<string> GenerateIsdocXmlAsync(InvoiceExtractionResult extraction);
}

public class IsdocGeneratorService : IIsdocGeneratorService
{
    private readonly ILogger<IsdocGeneratorService> _logger;
    private static readonly XNamespace IsdocNamespace = "http://isdoc.cz/namespace/2013";

    public IsdocGeneratorService(ILogger<IsdocGeneratorService> logger)
    {
        _logger = logger;
    }

    public Task<string> GenerateIsdocXmlAsync(InvoiceExtractionResult extraction)
    {
        try
        {
            _logger.LogInformation("Generating ISDOC XML for invoice: {InvoiceNumber}", extraction.InvoiceNumber);

            // Create root element with namespace
            var invoice = new XElement(IsdocNamespace + "Invoice",
                new XAttribute("version", "6.0.2")
            );

            // Add document type and basic info
            AddBasicInformation(invoice, extraction);

            // Add parties (supplier and customer)
            AddAccountingSupplierParty(invoice, extraction.Supplier);
            AddAccountingCustomerParty(invoice, extraction.Customer);

            // Add invoice lines
            if (extraction.Lines.Any())
            {
                AddInvoiceLines(invoice, extraction.Lines, extraction.Currency ?? "CZK");
            }

            // Add tax totals
            AddTaxTotal(invoice, extraction);

            // Add legal monetary totals
            AddLegalMonetaryTotal(invoice, extraction);

            // Add payment means (if available)
            if (!string.IsNullOrEmpty(extraction.BankAccount) || !string.IsNullOrEmpty(extraction.Iban))
            {
                AddPaymentMeans(invoice, extraction);
            }

            // Generate XML document
            var document = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                invoice
            );

            var xmlContent = document.ToString(SaveOptions.None);
            _logger.LogInformation("ISDOC XML generated successfully for invoice: {InvoiceNumber}", extraction.InvoiceNumber);

            return Task.FromResult(xmlContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating ISDOC XML for invoice: {InvoiceNumber}", extraction.InvoiceNumber);
            throw;
        }
    }

    private void AddBasicInformation(XElement invoice, InvoiceExtractionResult extraction)
    {
        // Document type (1 = invoice)
        invoice.Add(new XElement(IsdocNamespace + "DocumentType", extraction.DocumentType ?? "1"));

        // Invoice number
        invoice.Add(new XElement(IsdocNamespace + "ID", extraction.InvoiceNumber ?? "UNKNOWN"));

        // UUID
        invoice.Add(new XElement(IsdocNamespace + "UUID", Guid.NewGuid().ToString()));

        // Issue date
        if (extraction.IssuedOn.HasValue)
        {
            invoice.Add(new XElement(IsdocNamespace + "IssueDate",
                extraction.IssuedOn.Value.ToString("yyyy-MM-dd")));
        }

        // Tax point date (DUZP) — fall back to issue date if missing (common on foreign invoices)
        if (extraction.TaxableSupplyDate.HasValue)
        {
            invoice.Add(new XElement(IsdocNamespace + "TaxPointDate",
                extraction.TaxableSupplyDate.Value.ToString("yyyy-MM-dd")));
        }
        else if (extraction.IssuedOn.HasValue)
        {
            invoice.Add(new XElement(IsdocNamespace + "TaxPointDate",
                extraction.IssuedOn.Value.ToString("yyyy-MM-dd")));
        }

        // VATApplicable: true when the invoice has any VAT-bearing line, false for foreign/non-VAT
        var vatApplicable = extraction.VatRates.Any(r => r.VatRate > 0);
        invoice.Add(new XElement(IsdocNamespace + "VATApplicable", vatApplicable ? "true" : "false"));

        // ElectronicPossibilityAgreementReference is required by XSD; empty string is acceptable
        invoice.Add(new XElement(IsdocNamespace + "ElectronicPossibilityAgreementReference", string.Empty));

        // Note (optional)
        if (!string.IsNullOrEmpty(extraction.Note))
        {
            invoice.Add(new XElement(IsdocNamespace + "Note", extraction.Note));
        }

        // Currency
        var localCurrency = extraction.Currency ?? "CZK";
        invoice.Add(new XElement(IsdocNamespace + "LocalCurrencyCode", localCurrency));
        if (localCurrency != "CZK")
        {
            invoice.Add(new XElement(IsdocNamespace + "ForeignCurrencyCode", localCurrency));
        }
        invoice.Add(new XElement(IsdocNamespace + "CurrRate", "1"));
        invoice.Add(new XElement(IsdocNamespace + "RefCurrRate", "1"));
    }

    private void AddAccountingSupplierParty(XElement invoice, PartyDetails supplier)
    {
        var supplierParty = new XElement(IsdocNamespace + "AccountingSupplierParty");
        var party = new XElement(IsdocNamespace + "Party");

        // Party identification
        var partyIdentification = new XElement(IsdocNamespace + "PartyIdentification");

        if (!string.IsNullOrEmpty(supplier.RegistrationNo))
        {
            partyIdentification.Add(new XElement(IsdocNamespace + "ID",
                new XAttribute("schemeID", "ICO"),
                supplier.RegistrationNo));
        }

        if (!string.IsNullOrEmpty(supplier.VatNo))
        {
            var vatId = new XElement(IsdocNamespace + "ID", supplier.VatNo);
            partyIdentification.Add(vatId);
        }

        if (partyIdentification.HasElements)
        {
            party.Add(partyIdentification);
        }

        // Party name
        if (!string.IsNullOrEmpty(supplier.Name))
        {
            party.Add(new XElement(IsdocNamespace + "PartyName",
                new XElement(IsdocNamespace + "Name", supplier.Name)));
        }

        // Postal address
        var hasAddress = !string.IsNullOrEmpty(supplier.Street) ||
                        !string.IsNullOrEmpty(supplier.City) ||
                        !string.IsNullOrEmpty(supplier.Zip);

        if (hasAddress)
        {
            var postalAddress = new XElement(IsdocNamespace + "PostalAddress");

            if (!string.IsNullOrEmpty(supplier.Street))
            {
                postalAddress.Add(new XElement(IsdocNamespace + "StreetName", supplier.Street));
            }

            if (!string.IsNullOrEmpty(supplier.City))
            {
                postalAddress.Add(new XElement(IsdocNamespace + "CityName", supplier.City));
            }

            if (!string.IsNullOrEmpty(supplier.Zip))
            {
                postalAddress.Add(new XElement(IsdocNamespace + "PostalZone", supplier.Zip));
            }

            if (!string.IsNullOrEmpty(supplier.Country))
            {
                postalAddress.Add(new XElement(IsdocNamespace + "Country",
                    new XElement(IsdocNamespace + "IdentificationCode", supplier.Country)));
            }

            party.Add(postalAddress);
        }

        supplierParty.Add(party);
        invoice.Add(supplierParty);
    }

    private void AddAccountingCustomerParty(XElement invoice, PartyDetails customer)
    {
        var customerParty = new XElement(IsdocNamespace + "AccountingCustomerParty");
        var party = new XElement(IsdocNamespace + "Party");

        // Party identification
        var partyIdentification = new XElement(IsdocNamespace + "PartyIdentification");

        if (!string.IsNullOrEmpty(customer.RegistrationNo))
        {
            partyIdentification.Add(new XElement(IsdocNamespace + "ID",
                new XAttribute("schemeID", "ICO"),
                customer.RegistrationNo));
        }

        if (!string.IsNullOrEmpty(customer.VatNo))
        {
            var vatId = new XElement(IsdocNamespace + "ID", customer.VatNo);
            partyIdentification.Add(vatId);
        }

        if (partyIdentification.HasElements)
        {
            party.Add(partyIdentification);
        }

        // Party name
        if (!string.IsNullOrEmpty(customer.Name))
        {
            party.Add(new XElement(IsdocNamespace + "PartyName",
                new XElement(IsdocNamespace + "Name", customer.Name)));
        }

        // Postal address
        var hasAddress = !string.IsNullOrEmpty(customer.Street) ||
                        !string.IsNullOrEmpty(customer.City) ||
                        !string.IsNullOrEmpty(customer.Zip);

        if (hasAddress)
        {
            var postalAddress = new XElement(IsdocNamespace + "PostalAddress");

            if (!string.IsNullOrEmpty(customer.Street))
            {
                postalAddress.Add(new XElement(IsdocNamespace + "StreetName", customer.Street));
            }

            if (!string.IsNullOrEmpty(customer.City))
            {
                postalAddress.Add(new XElement(IsdocNamespace + "CityName", customer.City));
            }

            if (!string.IsNullOrEmpty(customer.Zip))
            {
                postalAddress.Add(new XElement(IsdocNamespace + "PostalZone", customer.Zip));
            }

            if (!string.IsNullOrEmpty(customer.Country))
            {
                postalAddress.Add(new XElement(IsdocNamespace + "Country",
                    new XElement(IsdocNamespace + "IdentificationCode", customer.Country)));
            }

            party.Add(postalAddress);
        }

        customerParty.Add(party);
        invoice.Add(customerParty);
    }

    private void AddInvoiceLines(XElement invoice, List<InvoiceLineItem> lines, string currency)
    {
        var invoiceLines = new XElement(IsdocNamespace + "InvoiceLines");

        foreach (var line in lines.OrderBy(l => l.LineNumber))
        {
            var invoiceLine = new XElement(IsdocNamespace + "InvoiceLine");

            // Line ID
            invoiceLine.Add(new XElement(IsdocNamespace + "ID", line.LineNumber));

            // Quantity
            if (line.Quantity > 0)
            {
                var quantityElement = new XElement(IsdocNamespace + "InvoicedQuantity",
                    FormatDecimal(line.Quantity));

                if (!string.IsNullOrEmpty(line.UnitName))
                {
                    quantityElement.Add(new XAttribute("unitCode", line.UnitName));
                }

                invoiceLine.Add(quantityElement);
            }

            // Line extension amount (total without VAT)
            invoiceLine.Add(new XElement(IsdocNamespace + "LineExtensionAmount",
                FormatDecimal(line.TotalPriceWithoutVat)));

            // Line extension amount tax inclusive (total with VAT)
            invoiceLine.Add(new XElement(IsdocNamespace + "LineExtensionAmountTaxInclusive",
                FormatDecimal(line.TotalPriceWithVat)));

            // Unit price
            if (line.UnitPrice > 0)
            {
                invoiceLine.Add(new XElement(IsdocNamespace + "UnitPrice",
                    FormatDecimal(line.UnitPrice)));
            }

            // Unit price tax inclusive
            if (line.Quantity > 0)
            {
                var unitPriceWithVat = line.TotalPriceWithVat / line.Quantity;
                invoiceLine.Add(new XElement(IsdocNamespace + "UnitPriceTaxInclusive",
                    FormatDecimal(unitPriceWithVat)));
            }

            // Tax category
            var taxCategory = new XElement(IsdocNamespace + "ClassifiedTaxCategory");
            taxCategory.Add(new XElement(IsdocNamespace + "Percent", FormatDecimal(line.VatRate)));
            taxCategory.Add(new XElement(IsdocNamespace + "VATCalculationMethod", "0")); // 0 = from base
            invoiceLine.Add(taxCategory);

            // Item description
            var item = new XElement(IsdocNamespace + "Item");
            item.Add(new XElement(IsdocNamespace + "Description", line.Name));

            if (!string.IsNullOrEmpty(line.Sku))
            {
                item.Add(new XElement(IsdocNamespace + "SellersItemIdentification",
                    new XElement(IsdocNamespace + "ID", line.Sku)));
            }

            invoiceLine.Add(item);
            invoiceLines.Add(invoiceLine);
        }

        invoice.Add(invoiceLines);
    }

    private void AddTaxTotal(XElement invoice, InvoiceExtractionResult extraction)
    {
        var taxTotal = new XElement(IsdocNamespace + "TaxTotal");

        // Total tax amount
        var totalTax = extraction.VatRates.Sum(v => v.Vat);
        taxTotal.Add(new XElement(IsdocNamespace + "TaxAmount", FormatDecimal(totalTax)));

        // Tax subtotals per rate
        foreach (var vatRate in extraction.VatRates)
        {
            var taxSubTotal = new XElement(IsdocNamespace + "TaxSubTotal");
            taxSubTotal.Add(new XElement(IsdocNamespace + "TaxableAmount", FormatDecimal(vatRate.Base)));
            taxSubTotal.Add(new XElement(IsdocNamespace + "TaxAmount", FormatDecimal(vatRate.Vat)));

            var taxCategory = new XElement(IsdocNamespace + "TaxCategory");
            taxCategory.Add(new XElement(IsdocNamespace + "Percent", FormatDecimal(vatRate.VatRate)));
            taxSubTotal.Add(taxCategory);

            taxTotal.Add(taxSubTotal);
        }

        invoice.Add(taxTotal);
    }

    private void AddLegalMonetaryTotal(XElement invoice, InvoiceExtractionResult extraction)
    {
        var legalMonetaryTotal = new XElement(IsdocNamespace + "LegalMonetaryTotal");

        // Tax exclusive amount (subtotal)
        legalMonetaryTotal.Add(new XElement(IsdocNamespace + "TaxExclusiveAmount",
            FormatDecimal(extraction.Subtotal ?? 0)));

        // Tax inclusive amount (total)
        legalMonetaryTotal.Add(new XElement(IsdocNamespace + "TaxInclusiveAmount",
            FormatDecimal(extraction.Total ?? 0)));

        // Already claimed amounts (for advance payments - default to 0)
        legalMonetaryTotal.Add(new XElement(IsdocNamespace + "AlreadyClaimedTaxExclusiveAmount", "0"));
        legalMonetaryTotal.Add(new XElement(IsdocNamespace + "AlreadyClaimedTaxInclusiveAmount", "0"));

        // Difference amounts (same as totals if no advance payments)
        legalMonetaryTotal.Add(new XElement(IsdocNamespace + "DifferenceTaxExclusiveAmount",
            FormatDecimal(extraction.Subtotal ?? 0)));
        legalMonetaryTotal.Add(new XElement(IsdocNamespace + "DifferenceTaxInclusiveAmount",
            FormatDecimal(extraction.Total ?? 0)));

        // Payable rounding amount (default to 0)
        legalMonetaryTotal.Add(new XElement(IsdocNamespace + "PayableRoundingAmount", "0"));

        // Paid deposits amount (default to 0)
        legalMonetaryTotal.Add(new XElement(IsdocNamespace + "PaidDepositsAmount", "0"));

        // Payable amount (amount to pay)
        legalMonetaryTotal.Add(new XElement(IsdocNamespace + "PayableAmount",
            FormatDecimal(extraction.Total ?? 0)));

        invoice.Add(legalMonetaryTotal);
    }

    private void AddPaymentMeans(XElement invoice, InvoiceExtractionResult extraction)
    {
        // PaymentType requires PaidAmount + PaymentMeansCode; Details is optional.
        var payment = new XElement(IsdocNamespace + "Payment",
            new XElement(IsdocNamespace + "PaidAmount", FormatDecimal(extraction.Total ?? 0)),
            new XElement(IsdocNamespace + "PaymentMeansCode", "42")); // 42 = money transfer

        // Details/money-transfer choice: PaymentDueDate + BankAccount group (ID, BankCode, Name, IBAN, BIC)
        // are all required by the XSD. Only emit Details when we can satisfy the BankAccount group.
        var (bankId, bankCode) = ParseCzechBankAccount(extraction.BankAccount);
        var iban = extraction.Iban ?? string.Empty;
        var canEmitBankAccount = !string.IsNullOrEmpty(bankId) && !string.IsNullOrEmpty(bankCode);

        if (extraction.DueOn.HasValue && canEmitBankAccount)
        {
            var details = new XElement(IsdocNamespace + "Details",
                new XElement(IsdocNamespace + "PaymentDueDate", extraction.DueOn.Value.ToString("yyyy-MM-dd")),
                new XElement(IsdocNamespace + "ID", bankId),
                new XElement(IsdocNamespace + "BankCode", bankCode),
                new XElement(IsdocNamespace + "Name", string.Empty),
                new XElement(IsdocNamespace + "IBAN", iban),
                new XElement(IsdocNamespace + "BIC", string.Empty));

            if (!string.IsNullOrEmpty(extraction.VariableSymbol))
                details.Add(new XElement(IsdocNamespace + "VariableSymbol", extraction.VariableSymbol));
            if (!string.IsNullOrEmpty(extraction.ConstantSymbol))
                details.Add(new XElement(IsdocNamespace + "ConstantSymbol", extraction.ConstantSymbol));
            if (!string.IsNullOrEmpty(extraction.SpecificSymbol))
                details.Add(new XElement(IsdocNamespace + "SpecificSymbol", extraction.SpecificSymbol));

            payment.Add(details);
        }

        invoice.Add(new XElement(IsdocNamespace + "PaymentMeans", payment));
    }

    private static (string bankId, string bankCode) ParseCzechBankAccount(string? account)
    {
        if (string.IsNullOrWhiteSpace(account)) return (string.Empty, string.Empty);
        var slash = account.IndexOf('/');
        if (slash <= 0 || slash == account.Length - 1) return (account.Trim(), string.Empty);
        return (account[..slash].Trim(), account[(slash + 1)..].Trim());
    }

    private string FormatDecimal(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
