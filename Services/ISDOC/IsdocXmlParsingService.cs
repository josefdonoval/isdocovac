using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Isdocovac.Models.OpenAI;
using Isdocovac.Providers;

namespace Isdocovac.Services.ISDOC;

public interface IIsdocXmlParsingService
{
    Task ProcessIsdocXmlAsync(Guid parsedInvoiceId, Guid processingId);
}

public class IsdocXmlParsingService : IIsdocXmlParsingService
{
    private static readonly XNamespace Ns = "http://isdoc.cz/namespace/2013";

    private readonly IParsedInvoiceProvider _parsedInvoiceProvider;
    private readonly IParsedInvoiceProcessingProvider _processingProvider;
    private readonly IIsdocValidationService _validationService;
    private readonly ILogger<IsdocXmlParsingService> _logger;

    public IsdocXmlParsingService(
        IParsedInvoiceProvider parsedInvoiceProvider,
        IParsedInvoiceProcessingProvider processingProvider,
        IIsdocValidationService validationService,
        ILogger<IsdocXmlParsingService> logger)
    {
        _parsedInvoiceProvider = parsedInvoiceProvider;
        _processingProvider = processingProvider;
        _validationService = validationService;
        _logger = logger;
    }

    public async Task ProcessIsdocXmlAsync(Guid parsedInvoiceId, Guid processingId)
    {
        _logger.LogInformation("Starting ISDOC XML parsing for invoice {ParsedInvoiceId}", parsedInvoiceId);

        try
        {
            // Mark as in progress
            await _processingProvider.UpdateProcessingStatusAsync(processingId, ProcessingStatus.InProgress);
            await _parsedInvoiceProvider.UpdateStatusAsync(parsedInvoiceId, ParsedInvoiceStatus.Parsing);

            // Download XML content from blob storage
            await _processingProvider.UpdateCurrentStepAsync(processingId, "LoadingXml");
            string xmlContent;
            using (var stream = await _parsedInvoiceProvider.GetFileContentAsync(parsedInvoiceId))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                xmlContent = await reader.ReadToEndAsync();
            }

            // Validate against ISDOC XSD schema
            await _processingProvider.UpdateCurrentStepAsync(processingId, "ValidatingXSD");
            var (isValid, validationErrors) = await _validationService.ValidateAgainstXsdAsync(xmlContent);

            if (!isValid)
            {
                _logger.LogWarning("ISDOC XML validation failed for invoice {ParsedInvoiceId} with {ErrorCount} errors",
                    parsedInvoiceId, validationErrors.Count);

                var errorsText = string.Join("\n", validationErrors);
                var failedInvoice = new ParsedInvoice
                {
                    IsValid = false,
                    ValidationErrors = errorsText
                };
                await _parsedInvoiceProvider.UpdateParsedDataAsync(parsedInvoiceId, failedInvoice);
                await _parsedInvoiceProvider.UpdateStatusAsync(parsedInvoiceId, ParsedInvoiceStatus.ValidationFailed);
                await _processingProvider.FailProcessingAsync(processingId, $"ISDOC XML validation failed: {errorsText}");
                return;
            }

            // Parse XML content
            await _processingProvider.UpdateCurrentStepAsync(processingId, "ParsingData");
            var extractedData = ParseIsdocXml(xmlContent);

            // Build the update object
            var updatedInvoice = new ParsedInvoice
            {
                InvoiceNumber = extractedData.InvoiceNumber,
                DocumentType = extractedData.DocumentType,
                IssuedOn = extractedData.IssuedOn,
                DueOn = extractedData.DueOn,
                Currency = extractedData.Currency,

                SupplierName = extractedData.Supplier.Name,
                SupplierStreet = extractedData.Supplier.Street,
                SupplierCity = extractedData.Supplier.City,
                SupplierZip = extractedData.Supplier.Zip,
                SupplierCountry = extractedData.Supplier.Country,
                SupplierRegistrationNo = extractedData.Supplier.RegistrationNo,
                SupplierVatNo = extractedData.Supplier.VatNo,

                CustomerName = extractedData.Customer.Name,
                CustomerStreet = extractedData.Customer.Street,
                CustomerCity = extractedData.Customer.City,
                CustomerZip = extractedData.Customer.Zip,
                CustomerCountry = extractedData.Customer.Country,
                CustomerRegistrationNo = extractedData.Customer.RegistrationNo,
                CustomerVatNo = extractedData.Customer.VatNo,

                Subtotal = extractedData.Subtotal,
                Total = extractedData.Total,
                VatPriceMode = extractedData.VatPriceMode,

                VariableSymbol = extractedData.VariableSymbol,
                ConstantSymbol = extractedData.ConstantSymbol,
                SpecificSymbol = extractedData.SpecificSymbol,
                BankAccount = extractedData.BankAccount,
                Iban = extractedData.Iban,
                Note = extractedData.Note,

                IsValid = true,
                ValidationErrors = null,
                LinesJson = JsonSerializer.Serialize(extractedData.Lines),
                VatRatesSummary = JsonSerializer.Serialize(extractedData.VatRates)
            };

            await _parsedInvoiceProvider.UpdateParsedDataAsync(parsedInvoiceId, updatedInvoice);
            await _parsedInvoiceProvider.UpdateTaxableSupplyDateAsync(parsedInvoiceId, extractedData.TaxableSupplyDate);
            await _parsedInvoiceProvider.UpdateStatusAsync(parsedInvoiceId, ParsedInvoiceStatus.Parsed);
            await _processingProvider.CompleteProcessingAsync(processingId);

            _logger.LogInformation("ISDOC XML parsing completed successfully for invoice {ParsedInvoiceId}", parsedInvoiceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing ISDOC XML for invoice {ParsedInvoiceId}", parsedInvoiceId);
            await _parsedInvoiceProvider.UpdateStatusAsync(parsedInvoiceId, ParsedInvoiceStatus.ValidationFailed);
            await _processingProvider.FailProcessingAsync(processingId, $"Parsing error: {ex.Message}");
        }
    }

    private InvoiceExtractionResult ParseIsdocXml(string xmlContent)
    {
        var doc = XDocument.Parse(xmlContent);
        var root = doc.Root ?? throw new InvalidOperationException("XML document has no root element");

        var result = new InvoiceExtractionResult { Success = true };

        // Basic invoice info
        result.InvoiceNumber = root.Element(Ns + "ID")?.Value;
        result.DocumentType = root.Element(Ns + "DocumentType")?.Value;

        result.IssuedOn = ParseDateUtc(root.Element(Ns + "IssueDate")?.Value);
        result.TaxableSupplyDate = ParseDateUtc(root.Element(Ns + "TaxPointDate")?.Value);
        result.DueOn = ParseDateUtc(root.Element(Ns + "DueDate")?.Value);

        result.Currency = root.Element(Ns + "LocalCurrencyCode")?.Value;

        // Supplier
        result.Supplier = ParseParty(root.Element(Ns + "AccountingSupplierParty")?.Element(Ns + "Party"));

        // Customer
        result.Customer = ParseParty(root.Element(Ns + "AccountingCustomerParty")?.Element(Ns + "Party"));

        // Invoice lines
        result.Lines = ParseInvoiceLines(root.Element(Ns + "InvoiceLines"));

        // Tax totals
        result.VatRates = ParseVatRates(root.Element(Ns + "TaxTotal"));

        // Legal monetary totals
        var lmt = root.Element(Ns + "LegalMonetaryTotal");
        if (lmt != null)
        {
            result.Subtotal = ParseDecimal(lmt.Element(Ns + "TaxExclusiveAmount")?.Value);
            result.Total = ParseDecimal(lmt.Element(Ns + "TaxInclusiveAmount")?.Value)
                        ?? ParseDecimal(lmt.Element(Ns + "PayableAmount")?.Value);
        }

        // Payment means
        ParsePaymentMeans(root.Element(Ns + "PaymentMeans"), result);

        // Note
        result.Note = root.Element(Ns + "Note")?.Value;

        return result;
    }

    private PartyDetails ParseParty(XElement? party)
    {
        var details = new PartyDetails();
        if (party == null) return details;

        // Name
        details.Name = party.Element(Ns + "PartyName")?.Element(Ns + "Name")?.Value;

        // Registration numbers from PartyIdentification
        foreach (var id in party.Elements(Ns + "PartyIdentification").SelectMany(pi => pi.Elements(Ns + "ID")))
        {
            var schemeId = id.Attribute("schemeID")?.Value;
            if (schemeId == "ICO")
                details.RegistrationNo = id.Value;
            else if (string.IsNullOrEmpty(schemeId) && string.IsNullOrEmpty(details.VatNo))
                details.VatNo = id.Value;
        }

        // Also check for CompanyID / PartyTaxScheme patterns used in some ISDOC files
        if (string.IsNullOrEmpty(details.VatNo))
        {
            details.VatNo = party.Element(Ns + "PartyTaxScheme")?
                .Element(Ns + "CompanyID")?.Value;
        }

        // Postal address
        var address = party.Element(Ns + "PostalAddress");
        if (address != null)
        {
            details.Street = address.Element(Ns + "StreetName")?.Value;
            details.City = address.Element(Ns + "CityName")?.Value;
            details.Zip = address.Element(Ns + "PostalZone")?.Value;
            details.Country = address.Element(Ns + "Country")?
                .Element(Ns + "IdentificationCode")?.Value;
        }

        return details;
    }

    private List<InvoiceLineItem> ParseInvoiceLines(XElement? invoiceLinesEl)
    {
        var lines = new List<InvoiceLineItem>();
        if (invoiceLinesEl == null) return lines;

        foreach (var lineEl in invoiceLinesEl.Elements(Ns + "InvoiceLine"))
        {
            var line = new InvoiceLineItem();

            if (int.TryParse(lineEl.Element(Ns + "ID")?.Value, out var lineId))
                line.LineNumber = lineId;

            var qtyEl = lineEl.Element(Ns + "InvoicedQuantity");
            line.Quantity = ParseDecimal(qtyEl?.Value) ?? 0;
            line.UnitName = qtyEl?.Attribute("unitCode")?.Value;

            line.TotalPriceWithoutVat = ParseDecimal(lineEl.Element(Ns + "LineExtensionAmount")?.Value) ?? 0;
            line.TotalPriceWithVat = ParseDecimal(lineEl.Element(Ns + "LineExtensionAmountTaxInclusive")?.Value) ?? 0;
            line.TotalVat = line.TotalPriceWithVat - line.TotalPriceWithoutVat;
            line.UnitPrice = ParseDecimal(lineEl.Element(Ns + "UnitPrice")?.Value) ?? 0;

            var taxCategory = lineEl.Element(Ns + "ClassifiedTaxCategory");
            line.VatRate = ParseDecimal(taxCategory?.Element(Ns + "Percent")?.Value) ?? 0;

            var item = lineEl.Element(Ns + "Item");
            line.Name = item?.Element(Ns + "Description")?.Value ?? string.Empty;
            line.Sku = item?.Element(Ns + "SellersItemIdentification")?
                .Element(Ns + "ID")?.Value;

            lines.Add(line);
        }

        return lines;
    }

    private List<VatRateSummary> ParseVatRates(XElement? taxTotalEl)
    {
        var vatRates = new List<VatRateSummary>();
        if (taxTotalEl == null) return vatRates;

        foreach (var subTotal in taxTotalEl.Elements(Ns + "TaxSubTotal"))
        {
            var summary = new VatRateSummary
            {
                Base = ParseDecimal(subTotal.Element(Ns + "TaxableAmount")?.Value) ?? 0,
                Vat = ParseDecimal(subTotal.Element(Ns + "TaxAmount")?.Value) ?? 0,
                VatRate = ParseDecimal(subTotal.Element(Ns + "TaxCategory")?
                    .Element(Ns + "Percent")?.Value) ?? 0
            };
            vatRates.Add(summary);
        }

        return vatRates;
    }

    private void ParsePaymentMeans(XElement? paymentMeansEl, InvoiceExtractionResult result)
    {
        if (paymentMeansEl == null) return;

        var payment = paymentMeansEl.Element(Ns + "Payment");
        if (payment == null) return;

        // Variable symbol - generated files use PaidDepositsID, real ISDOC files may use VS
        result.VariableSymbol = payment.Element(Ns + "VS")?.Value
                             ?? payment.Element(Ns + "PaidDepositsID")?.Value;
        result.ConstantSymbol = payment.Element(Ns + "KS")?.Value;
        result.SpecificSymbol = payment.Element(Ns + "SS")?.Value;

        var details = payment.Element(Ns + "Details");
        if (details != null)
        {
            result.BankAccount = details.Element(Ns + "ID")?.Value;
            result.Iban = details.Element(Ns + "IBAN")?.Value;
        }
    }

    private static DateTime? ParseDateUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
            return DateTime.SpecifyKind(result, DateTimeKind.Utc);
        return null;
    }

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            return result;
        return null;
    }
}
