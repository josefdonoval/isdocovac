using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Isdocovac.Providers;
using Isdocovac.Services.ISDOC;

namespace Isdocovac.Tests.Services.ISDOC;

public class IsdocXmlParsingServiceTests
{
    private readonly Mock<IParsedInvoiceProvider> _parsedInvoiceProviderMock;
    private readonly Mock<IParsedInvoiceProcessingProvider> _processingProviderMock;
    private readonly Mock<IIsdocValidationService> _validationServiceMock;
    private readonly Mock<ILogger<IsdocXmlParsingService>> _loggerMock;
    private readonly IsdocXmlParsingService _sut;

    private static readonly string MinimalIsdocXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Invoice xmlns=""http://isdoc.cz/namespace/2013"" version=""6.0.2"">
    <DocumentType>1</DocumentType>
    <ID>FV-001</ID>
    <UUID>00000000-0000-0000-0000-000000000001</UUID>
    <IssueDate>2026-01-01</IssueDate>
    <TaxPointDate>2026-01-01</TaxPointDate>
    <DueDate>2026-02-01</DueDate>
    <LocalCurrencyCode>CZK</LocalCurrencyCode>
    <AccountingSupplierParty>
        <Party>
            <PartyName><Name>Supplier</Name></PartyName>
        </Party>
    </AccountingSupplierParty>
    <AccountingCustomerParty>
        <Party>
            <PartyName><Name>Customer</Name></PartyName>
        </Party>
    </AccountingCustomerParty>
    <InvoiceLines>
        <InvoiceLine>
            <ID>1</ID>
            <InvoicedQuantity>1</InvoicedQuantity>
            <LineExtensionAmount>1000</LineExtensionAmount>
            <LineExtensionAmountTaxInclusive>1210</LineExtensionAmountTaxInclusive>
            <UnitPrice>1000</UnitPrice>
            <UnitPriceTaxInclusive>1210</UnitPriceTaxInclusive>
            <ClassifiedTaxCategory>
                <Percent>21</Percent>
                <VATCalculationMethod>0</VATCalculationMethod>
            </ClassifiedTaxCategory>
            <Item><Description>Test Item</Description></Item>
        </InvoiceLine>
    </InvoiceLines>
    <TaxTotal>
        <TaxAmount>210</TaxAmount>
        <TaxSubTotal>
            <TaxableAmount>1000</TaxableAmount>
            <TaxAmount>210</TaxAmount>
            <TaxCategory>
                <Percent>21</Percent>
            </TaxCategory>
        </TaxSubTotal>
    </TaxTotal>
    <LegalMonetaryTotal>
        <TaxExclusiveAmount>1000</TaxExclusiveAmount>
        <TaxInclusiveAmount>1210</TaxInclusiveAmount>
        <AlreadyClaimedTaxExclusiveAmount>0</AlreadyClaimedTaxExclusiveAmount>
        <AlreadyClaimedTaxInclusiveAmount>0</AlreadyClaimedTaxInclusiveAmount>
        <DifferenceTaxExclusiveAmount>1000</DifferenceTaxExclusiveAmount>
        <DifferenceTaxInclusiveAmount>1210</DifferenceTaxInclusiveAmount>
        <PayableRoundingAmount>0</PayableRoundingAmount>
        <PaidDepositsAmount>0</PaidDepositsAmount>
        <PayableAmount>1210</PayableAmount>
    </LegalMonetaryTotal>
</Invoice>";

    public IsdocXmlParsingServiceTests()
    {
        _parsedInvoiceProviderMock = new Mock<IParsedInvoiceProvider>();
        _processingProviderMock = new Mock<IParsedInvoiceProcessingProvider>();
        _validationServiceMock = new Mock<IIsdocValidationService>();
        _loggerMock = new Mock<ILogger<IsdocXmlParsingService>>();

        _sut = new IsdocXmlParsingService(
            _parsedInvoiceProviderMock.Object,
            _processingProviderMock.Object,
            _validationServiceMock.Object,
            _loggerMock.Object);
    }

    private Stream CreateStreamFromString(string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    [Fact]
    public async Task ProcessIsdocXmlAsync_ValidXml_ParsesAndCompletesProcessing()
    {
        var parsedInvoiceId = Guid.NewGuid();
        var processingId = Guid.NewGuid();

        _parsedInvoiceProviderMock
            .Setup(x => x.GetFileContentAsync(parsedInvoiceId))
            .ReturnsAsync(CreateStreamFromString(MinimalIsdocXml));

        _validationServiceMock
            .Setup(x => x.ValidateAgainstXsdAsync(It.IsAny<string>()))
            .ReturnsAsync((true, new List<string>()));

        await _sut.ProcessIsdocXmlAsync(parsedInvoiceId, processingId);

        _processingProviderMock.Verify(
            x => x.CompleteProcessingAsync(processingId), Times.Once);
        _parsedInvoiceProviderMock.Verify(
            x => x.UpdateStatusAsync(parsedInvoiceId, ParsedInvoiceStatus.Parsed), Times.Once);
    }

    [Fact]
    public async Task ProcessIsdocXmlAsync_XsdValidationFails_SetsValidationFailedAndFailsProcessing()
    {
        var parsedInvoiceId = Guid.NewGuid();
        var processingId = Guid.NewGuid();
        var validationErrors = new List<string> { "Invalid element found" };

        _parsedInvoiceProviderMock
            .Setup(x => x.GetFileContentAsync(parsedInvoiceId))
            .ReturnsAsync(CreateStreamFromString(MinimalIsdocXml));

        _validationServiceMock
            .Setup(x => x.ValidateAgainstXsdAsync(It.IsAny<string>()))
            .ReturnsAsync((false, validationErrors));

        await _sut.ProcessIsdocXmlAsync(parsedInvoiceId, processingId);

        _parsedInvoiceProviderMock.Verify(
            x => x.UpdateStatusAsync(parsedInvoiceId, ParsedInvoiceStatus.ValidationFailed), Times.Once);
        _processingProviderMock.Verify(
            x => x.FailProcessingAsync(processingId, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ProcessIsdocXmlAsync_ExceptionDuringProcessing_SetsValidationFailedAndFails()
    {
        var parsedInvoiceId = Guid.NewGuid();
        var processingId = Guid.NewGuid();

        _parsedInvoiceProviderMock
            .Setup(x => x.GetFileContentAsync(parsedInvoiceId))
            .ThrowsAsync(new InvalidOperationException("Stream error"));

        await _sut.ProcessIsdocXmlAsync(parsedInvoiceId, processingId);

        _parsedInvoiceProviderMock.Verify(
            x => x.UpdateStatusAsync(parsedInvoiceId, ParsedInvoiceStatus.ValidationFailed), Times.Once);
        _processingProviderMock.Verify(
            x => x.FailProcessingAsync(processingId, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ProcessIsdocXmlAsync_UpdatesStepsInCorrectOrder()
    {
        var parsedInvoiceId = Guid.NewGuid();
        var processingId = Guid.NewGuid();
        var steps = new List<string>();

        _parsedInvoiceProviderMock
            .Setup(x => x.GetFileContentAsync(parsedInvoiceId))
            .ReturnsAsync(CreateStreamFromString(MinimalIsdocXml));

        _validationServiceMock
            .Setup(x => x.ValidateAgainstXsdAsync(It.IsAny<string>()))
            .ReturnsAsync((true, new List<string>()));

        _processingProviderMock
            .Setup(x => x.UpdateCurrentStepAsync(processingId, It.IsAny<string>()))
            .Callback<Guid, string>((_, step) => steps.Add(step))
            .Returns(Task.CompletedTask);

        await _sut.ProcessIsdocXmlAsync(parsedInvoiceId, processingId);

        steps.Should().ContainInOrder("LoadingXml", "ValidatingXSD", "ParsingData");
        _processingProviderMock.Verify(
            x => x.UpdateProcessingStatusAsync(processingId, ProcessingStatus.InProgress), Times.Once);
    }
}
