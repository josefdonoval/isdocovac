using System.Xml.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Isdocovac.Models.OpenAI;
using Isdocovac.Services.ISDOC;

namespace Isdocovac.Tests.Services.ISDOC;

public class IsdocGeneratorServiceTests
{
    private static readonly XNamespace IsdocNamespace = "http://isdoc.cz/namespace/2013";

    private readonly Mock<ILogger<IsdocGeneratorService>> _loggerMock;
    private readonly IsdocGeneratorService _sut;

    public IsdocGeneratorServiceTests()
    {
        _loggerMock = new Mock<ILogger<IsdocGeneratorService>>();
        _sut = new IsdocGeneratorService(_loggerMock.Object);
    }

    private static InvoiceExtractionResult CreateFullExtraction()
    {
        return new InvoiceExtractionResult
        {
            Success = true,
            InvoiceNumber = "FV-2026-001",
            DocumentType = "1",
            IssuedOn = new DateTime(2026, 3, 15),
            DueOn = new DateTime(2026, 4, 15),
            TaxableSupplyDate = new DateTime(2026, 3, 10),
            Supplier = new PartyDetails
            {
                Name = "Supplier s.r.o.",
                Street = "Supplier Street 123",
                City = "Prague",
                Zip = "11000",
                Country = "CZ",
                RegistrationNo = "12345678",
                VatNo = "CZ12345678"
            },
            Customer = new PartyDetails
            {
                Name = "Customer a.s.",
                Street = "Customer Avenue 456",
                City = "Brno",
                Zip = "60200",
                Country = "CZ",
                RegistrationNo = "87654321",
                VatNo = "CZ87654321"
            },
            Lines = new List<InvoiceLineItem>
            {
                new InvoiceLineItem
                {
                    LineNumber = 1,
                    Name = "Service A",
                    Quantity = 2m,
                    UnitName = "ks",
                    UnitPrice = 1000m,
                    VatRate = 21m,
                    TotalPriceWithoutVat = 2000m,
                    TotalVat = 420m,
                    TotalPriceWithVat = 2420m
                }
            },
            Subtotal = 2000m,
            Total = 2420m,
            Currency = "CZK",
            VatRates = new List<VatRateSummary>
            {
                new VatRateSummary { VatRate = 21m, Base = 2000m, Vat = 420m }
            },
            VariableSymbol = "2026001",
            BankAccount = "123456789/0100",
            Iban = "CZ6508000000000123456789",
            Note = "Payment note"
        };
    }

    private static InvoiceExtractionResult CreateMinimalExtraction()
    {
        return new InvoiceExtractionResult
        {
            Success = true,
            InvoiceNumber = "FV-001",
            IssuedOn = new DateTime(2026, 1, 1),
            DueOn = new DateTime(2026, 2, 1),
            Lines = new List<InvoiceLineItem>(),
            VatRates = new List<VatRateSummary>(),
            Subtotal = 0m,
            Total = 0m
        };
    }

    [Fact]
    public async Task GenerateIsdocXmlAsync_GeneratesValidXmlWithCorrectNamespace()
    {
        var extraction = CreateFullExtraction();

        var xml = await _sut.GenerateIsdocXmlAsync(extraction);

        var doc = XDocument.Parse(xml);
        doc.Root.Should().NotBeNull();
        doc.Root!.Name.Namespace.NamespaceName.Should().Be("http://isdoc.cz/namespace/2013");
        doc.Root.Name.LocalName.Should().Be("Invoice");
    }

    [Fact]
    public async Task GenerateIsdocXmlAsync_HasVersionAttribute()
    {
        var extraction = CreateFullExtraction();

        var xml = await _sut.GenerateIsdocXmlAsync(extraction);

        var doc = XDocument.Parse(xml);
        var version = doc.Root!.Attribute("version")?.Value;
        version.Should().Be("6.0.2");
    }

    [Fact]
    public async Task GenerateIsdocXmlAsync_ContainsDocumentTypeElement()
    {
        var extraction = CreateFullExtraction();
        extraction.DocumentType = "2";

        var xml = await _sut.GenerateIsdocXmlAsync(extraction);

        var doc = XDocument.Parse(xml);
        var documentType = doc.Root!.Element(IsdocNamespace + "DocumentType");
        documentType.Should().NotBeNull();
        documentType!.Value.Should().Be("2");
    }

    [Fact]
    public async Task GenerateIsdocXmlAsync_ContainsInvoiceNumberAsId()
    {
        var extraction = CreateFullExtraction();

        var xml = await _sut.GenerateIsdocXmlAsync(extraction);

        var doc = XDocument.Parse(xml);
        var id = doc.Root!.Element(IsdocNamespace + "ID");
        id.Should().NotBeNull();
        id!.Value.Should().Be("FV-2026-001");
    }

    [Fact]
    public async Task GenerateIsdocXmlAsync_ContainsIssueDateWhenProvided()
    {
        var extraction = CreateFullExtraction();

        var xml = await _sut.GenerateIsdocXmlAsync(extraction);

        var doc = XDocument.Parse(xml);
        var issueDate = doc.Root!.Element(IsdocNamespace + "IssueDate");
        issueDate.Should().NotBeNull();
        issueDate!.Value.Should().Contain("2026-03-15");
    }

    [Fact]
    public async Task GenerateIsdocXmlAsync_TaxPointDateDefaultsToIssueDateWhenTaxableSupplyDateNull()
    {
        var extraction = CreateFullExtraction();
        extraction.TaxableSupplyDate = null;

        var xml = await _sut.GenerateIsdocXmlAsync(extraction);

        var doc = XDocument.Parse(xml);
        var issueDate = doc.Root!.Element(IsdocNamespace + "IssueDate")?.Value;
        var taxPointDate = doc.Root!.Element(IsdocNamespace + "TaxPointDate")?.Value;
        taxPointDate.Should().Be(issueDate);
    }

    [Fact]
    public async Task GenerateIsdocXmlAsync_ContainsSupplierPartyWithNameAndAddress()
    {
        var extraction = CreateFullExtraction();

        var xml = await _sut.GenerateIsdocXmlAsync(extraction);

        var doc = XDocument.Parse(xml);
        var supplierParty = doc.Root!.Element(IsdocNamespace + "AccountingSupplierParty");
        supplierParty.Should().NotBeNull();

        var party = supplierParty!.Element(IsdocNamespace + "Party");
        party.Should().NotBeNull();

        var partyName = party!.Element(IsdocNamespace + "PartyName")
            ?.Element(IsdocNamespace + "Name");
        partyName.Should().NotBeNull();
        partyName!.Value.Should().Be("Supplier s.r.o.");

        var postalAddress = party.Element(IsdocNamespace + "PostalAddress");
        postalAddress.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateIsdocXmlAsync_ContainsCustomerParty()
    {
        var extraction = CreateFullExtraction();

        var xml = await _sut.GenerateIsdocXmlAsync(extraction);

        var doc = XDocument.Parse(xml);
        var customerParty = doc.Root!.Element(IsdocNamespace + "AccountingCustomerParty");
        customerParty.Should().NotBeNull();

        var party = customerParty!.Element(IsdocNamespace + "Party");
        party.Should().NotBeNull();

        var partyName = party!.Element(IsdocNamespace + "PartyName")
            ?.Element(IsdocNamespace + "Name");
        partyName.Should().NotBeNull();
        partyName!.Value.Should().Be("Customer a.s.");
    }

    [Fact]
    public async Task GenerateIsdocXmlAsync_ContainsInvoiceLinesWithCorrectAmounts()
    {
        var extraction = CreateFullExtraction();

        var xml = await _sut.GenerateIsdocXmlAsync(extraction);

        var doc = XDocument.Parse(xml);
        var invoiceLines = doc.Root!.Element(IsdocNamespace + "InvoiceLines");
        invoiceLines.Should().NotBeNull();

        var lines = invoiceLines!.Elements(IsdocNamespace + "InvoiceLine").ToList();
        lines.Should().HaveCount(1);

        var line = lines.First();
        var lineExtensionAmount = line.Element(IsdocNamespace + "LineExtensionAmount");
        lineExtensionAmount.Should().NotBeNull();
        lineExtensionAmount!.Value.Should().Be("2000");

        var lineExtensionAmountTaxInclusive = line.Element(IsdocNamespace + "LineExtensionAmountTaxInclusive");
        lineExtensionAmountTaxInclusive.Should().NotBeNull();
        lineExtensionAmountTaxInclusive!.Value.Should().Be("2420");
    }

    [Fact]
    public async Task GenerateIsdocXmlAsync_ContainsTaxTotalWithSubtotalsPerRate()
    {
        var extraction = CreateFullExtraction();

        var xml = await _sut.GenerateIsdocXmlAsync(extraction);

        var doc = XDocument.Parse(xml);
        var taxTotal = doc.Root!.Element(IsdocNamespace + "TaxTotal");
        taxTotal.Should().NotBeNull();

        var taxAmount = taxTotal!.Element(IsdocNamespace + "TaxAmount");
        taxAmount.Should().NotBeNull();

        var taxSubTotals = taxTotal.Elements(IsdocNamespace + "TaxSubTotal").ToList();
        taxSubTotals.Should().HaveCount(1);
    }

    [Fact]
    public async Task GenerateIsdocXmlAsync_ContainsLegalMonetaryTotalWithAllAmounts()
    {
        var extraction = CreateFullExtraction();

        var xml = await _sut.GenerateIsdocXmlAsync(extraction);

        var doc = XDocument.Parse(xml);
        var legalMonetaryTotal = doc.Root!.Element(IsdocNamespace + "LegalMonetaryTotal");
        legalMonetaryTotal.Should().NotBeNull();

        legalMonetaryTotal!.Element(IsdocNamespace + "TaxExclusiveAmount").Should().NotBeNull();
        legalMonetaryTotal.Element(IsdocNamespace + "TaxInclusiveAmount").Should().NotBeNull();
        legalMonetaryTotal.Element(IsdocNamespace + "AlreadyClaimedTaxExclusiveAmount").Should().NotBeNull();
        legalMonetaryTotal.Element(IsdocNamespace + "AlreadyClaimedTaxInclusiveAmount").Should().NotBeNull();
        legalMonetaryTotal.Element(IsdocNamespace + "DifferenceTaxExclusiveAmount").Should().NotBeNull();
        legalMonetaryTotal.Element(IsdocNamespace + "DifferenceTaxInclusiveAmount").Should().NotBeNull();
        legalMonetaryTotal.Element(IsdocNamespace + "PayableRoundingAmount").Should().NotBeNull();
        legalMonetaryTotal.Element(IsdocNamespace + "PaidDepositsAmount").Should().NotBeNull();
        legalMonetaryTotal.Element(IsdocNamespace + "PayableAmount").Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateIsdocXmlAsync_ContainsPaymentMeansWhenBankInfoPresent()
    {
        var extraction = CreateFullExtraction();

        var xml = await _sut.GenerateIsdocXmlAsync(extraction);

        var doc = XDocument.Parse(xml);
        var paymentMeans = doc.Root!.Element(IsdocNamespace + "PaymentMeans");
        paymentMeans.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateIsdocXmlAsync_OmitsPaymentMeansWhenNoBankInfo()
    {
        var extraction = CreateFullExtraction();
        extraction.BankAccount = null;
        extraction.Iban = null;

        var xml = await _sut.GenerateIsdocXmlAsync(extraction);

        var doc = XDocument.Parse(xml);
        var paymentMeans = doc.Root!.Element(IsdocNamespace + "PaymentMeans");
        paymentMeans.Should().BeNull();
    }

    [Fact]
    public async Task GenerateIsdocXmlAsync_ContainsNoteWhenProvided()
    {
        var extraction = CreateFullExtraction();

        var xml = await _sut.GenerateIsdocXmlAsync(extraction);

        var doc = XDocument.Parse(xml);
        var note = doc.Root!.Element(IsdocNamespace + "Note");
        note.Should().NotBeNull();
        note!.Value.Should().Be("Payment note");
    }

    [Fact]
    public async Task GenerateIsdocXmlAsync_FormatsDecimalsWithInvariantCulture()
    {
        var extraction = CreateFullExtraction();
        extraction.Subtotal = 1000.50m;
        extraction.Total = 1210.60m;

        var xml = await _sut.GenerateIsdocXmlAsync(extraction);

        var doc = XDocument.Parse(xml);
        var legalMonetaryTotal = doc.Root!.Element(IsdocNamespace + "LegalMonetaryTotal");
        var taxExclusive = legalMonetaryTotal!.Element(IsdocNamespace + "TaxExclusiveAmount")?.Value;
        var taxInclusive = legalMonetaryTotal.Element(IsdocNamespace + "TaxInclusiveAmount")?.Value;

        // Should use dot as decimal separator, no trailing zeros beyond significant digits
        taxExclusive.Should().Be("1000.5");
        taxInclusive.Should().Be("1210.6");
    }
}
