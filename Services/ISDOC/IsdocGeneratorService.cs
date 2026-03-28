using System.Globalization;
using System.Xml.Linq;
using Isdocovac.Models.OpenAI;

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

            // Add note if present
            if (!string.IsNullOrEmpty(extraction.Note))
            {
                invoice.Add(new XElement(IsdocNamespace + "Note", extraction.Note));
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

        // Tax point date (DUZP)
        if (extraction.TaxableSupplyDate.HasValue)
        {
            invoice.Add(new XElement(IsdocNamespace + "TaxPointDate",
                extraction.TaxableSupplyDate.Value.ToString("yyyy-MM-dd")));
        }
        else if (extraction.IssuedOn.HasValue)
        {
            // Default to issue date if not specified
            invoice.Add(new XElement(IsdocNamespace + "TaxPointDate",
                extraction.IssuedOn.Value.ToString("yyyy-MM-dd")));
        }

        // Due date
        if (extraction.DueOn.HasValue)
        {
            invoice.Add(new XElement(IsdocNamespace + "DueDate",
                extraction.DueOn.Value.ToString("yyyy-MM-dd")));
        }

        // Currency code
        invoice.Add(new XElement(IsdocNamespace + "LocalCurrencyCode", extraction.Currency ?? "CZK"));
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
        var paymentMeans = new XElement(IsdocNamespace + "PaymentMeans");
        var payment = new XElement(IsdocNamespace + "Payment");

        // Variable symbol
        if (!string.IsNullOrEmpty(extraction.VariableSymbol))
        {
            payment.Add(new XElement(IsdocNamespace + "PaidDepositsID", extraction.VariableSymbol));
        }

        // Bank account details
        var details = new XElement(IsdocNamespace + "Details");

        if (!string.IsNullOrEmpty(extraction.BankAccount))
        {
            details.Add(new XElement(IsdocNamespace + "ID", extraction.BankAccount));
        }

        if (!string.IsNullOrEmpty(extraction.Iban))
        {
            details.Add(new XElement(IsdocNamespace + "IBAN", extraction.Iban));
        }

        if (details.HasElements)
        {
            payment.Add(details);
        }

        if (payment.HasElements)
        {
            paymentMeans.Add(payment);
            invoice.Add(paymentMeans);
        }
    }

    private string FormatDecimal(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
