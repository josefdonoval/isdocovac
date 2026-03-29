using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Isdocovac.Providers;
using Isdocovac.Services;

namespace Isdocovac.Tests.Services;

public class InvoiceImportServiceTests
{
    private readonly Mock<IMainInvoiceProvider> _mainInvoiceProviderMock;
    private readonly Mock<IFakturoidInvoiceProvider> _fakturoidInvoiceProviderMock;
    private readonly Mock<IParsedInvoiceProvider> _parsedInvoiceProviderMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly InvoiceImportService _sut;

    public InvoiceImportServiceTests()
    {
        _mainInvoiceProviderMock = new Mock<IMainInvoiceProvider>();
        _fakturoidInvoiceProviderMock = new Mock<IFakturoidInvoiceProvider>();
        _parsedInvoiceProviderMock = new Mock<IParsedInvoiceProvider>();
        _configurationMock = new Mock<IConfiguration>();

        _sut = new InvoiceImportService(
            _mainInvoiceProviderMock.Object,
            _fakturoidInvoiceProviderMock.Object,
            _parsedInvoiceProviderMock.Object,
            _configurationMock.Object);
    }

    private static FakturoidInvoice CreateTestFakturoidInvoice(bool isImported = false)
    {
        return new FakturoidInvoice
        {
            Id = Guid.NewGuid(),
            FakturoidId = 12345,
            Number = "FV-2026-001",
            DocumentType = "invoice",
            IsImported = isImported,
            Lines = new List<FakturoidInvoiceLine>
            {
                new FakturoidInvoiceLine
                {
                    Id = Guid.NewGuid(),
                    LineOrder = 1,
                    Name = "Service",
                    Quantity = 1,
                    UnitPrice = 1000m,
                    VatRate = 21m,
                    TotalPriceWithoutVat = 1000m,
                    TotalVat = 210m,
                    TotalPriceWithVat = 1210m
                }
            },
            Payments = new List<FakturoidInvoicePayment>(),
            Attachments = new List<FakturoidInvoiceAttachment>()
        };
    }

    private static ParsedInvoice CreateTestParsedInvoice(
        ParsedInvoiceStatus status = ParsedInvoiceStatus.Parsed,
        bool isValid = true,
        Guid? importedInvoiceId = null)
    {
        return new ParsedInvoice
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            FileName = "test.isdoc",
            Status = status,
            IsValid = isValid,
            InvoiceNumber = "FV-001",
            SupplierVatNo = "CZ12345678",
            CustomerVatNo = "CZ87654321",
            ImportedInvoiceId = importedInvoiceId
        };
    }

    [Fact]
    public async Task ImportFromFakturoidAsync_ThrowsWhenNotFound()
    {
        _fakturoidInvoiceProviderMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((FakturoidInvoice?)null);

        var act = () => _sut.ImportFromFakturoidAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ImportFromFakturoidAsync_ThrowsWhenAlreadyImported()
    {
        var fakturoidInvoice = CreateTestFakturoidInvoice(isImported: true);

        _fakturoidInvoiceProviderMock
            .Setup(x => x.GetByIdAsync(fakturoidInvoice.Id))
            .ReturnsAsync(fakturoidInvoice);

        var act = () => _sut.ImportFromFakturoidAsync(fakturoidInvoice.Id, Guid.NewGuid());

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ImportFromFakturoidAsync_CreatesInvoiceWithCorrectMapping()
    {
        var fakturoidInvoice = CreateTestFakturoidInvoice();
        var userId = Guid.NewGuid();
        Invoice? capturedInvoice = null;

        _fakturoidInvoiceProviderMock
            .Setup(x => x.GetByIdAsync(fakturoidInvoice.Id))
            .ReturnsAsync(fakturoidInvoice);

        _mainInvoiceProviderMock
            .Setup(x => x.CreateAsync(It.IsAny<Invoice>()))
            .Callback<Invoice>(i => capturedInvoice = i)
            .ReturnsAsync((Invoice i) => i);

        await _sut.ImportFromFakturoidAsync(fakturoidInvoice.Id, userId);

        capturedInvoice.Should().NotBeNull();
        capturedInvoice!.Direction.Should().Be(InvoiceDirection.Outbound);
        capturedInvoice.Source.Should().Be(InvoiceSource.Fakturoid);
        capturedInvoice.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task ImportFromFakturoidAsync_MarksAsImported()
    {
        var fakturoidInvoice = CreateTestFakturoidInvoice();
        var userId = Guid.NewGuid();

        _fakturoidInvoiceProviderMock
            .Setup(x => x.GetByIdAsync(fakturoidInvoice.Id))
            .ReturnsAsync(fakturoidInvoice);

        _mainInvoiceProviderMock
            .Setup(x => x.CreateAsync(It.IsAny<Invoice>()))
            .ReturnsAsync((Invoice i) => i);

        await _sut.ImportFromFakturoidAsync(fakturoidInvoice.Id, userId);

        _fakturoidInvoiceProviderMock.Verify(
            x => x.MarkAsImportedAsync(fakturoidInvoice.Id, It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task ImportFromParsedInvoiceAsync_ThrowsWhenNotFound()
    {
        _parsedInvoiceProviderMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((ParsedInvoice?)null);

        var act = () => _sut.ImportFromParsedInvoiceAsync(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ImportFromParsedInvoiceAsync_ThrowsWhenAlreadyImported()
    {
        var parsedInvoice = CreateTestParsedInvoice(status: ParsedInvoiceStatus.Imported);

        _parsedInvoiceProviderMock
            .Setup(x => x.GetByIdAsync(parsedInvoice.Id))
            .ReturnsAsync(parsedInvoice);

        var act = () => _sut.ImportFromParsedInvoiceAsync(parsedInvoice.Id, Guid.NewGuid());

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ImportFromParsedInvoiceAsync_ThrowsWhenNotReady()
    {
        var parsedInvoice = CreateTestParsedInvoice(status: ParsedInvoiceStatus.Uploaded);

        _parsedInvoiceProviderMock
            .Setup(x => x.GetByIdAsync(parsedInvoice.Id))
            .ReturnsAsync(parsedInvoice);

        var act = () => _sut.ImportFromParsedInvoiceAsync(parsedInvoice.Id, Guid.NewGuid());

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ImportFromParsedInvoiceAsync_ThrowsWhenInvalid()
    {
        var parsedInvoice = CreateTestParsedInvoice(isValid: false);

        _parsedInvoiceProviderMock
            .Setup(x => x.GetByIdAsync(parsedInvoice.Id))
            .ReturnsAsync(parsedInvoice);

        var act = () => _sut.ImportFromParsedInvoiceAsync(parsedInvoice.Id, Guid.NewGuid());

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ImportFromParsedInvoiceAsync_DeterminesDirectionAsOutboundWhenSupplierVatMatches()
    {
        var parsedInvoice = CreateTestParsedInvoice();
        parsedInvoice.SupplierVatNo = "CZ99999999";
        var userId = Guid.NewGuid();
        Invoice? capturedInvoice = null;

        _configurationMock.Setup(c => c["Company:VatNo"]).Returns("CZ99999999");

        _parsedInvoiceProviderMock
            .Setup(x => x.GetByIdAsync(parsedInvoice.Id))
            .ReturnsAsync(parsedInvoice);

        _mainInvoiceProviderMock
            .Setup(x => x.CreateAsync(It.IsAny<Invoice>()))
            .Callback<Invoice>(i => capturedInvoice = i)
            .ReturnsAsync((Invoice i) => i);

        await _sut.ImportFromParsedInvoiceAsync(parsedInvoice.Id, userId);

        capturedInvoice.Should().NotBeNull();
        capturedInvoice!.Direction.Should().Be(InvoiceDirection.Outbound);
        capturedInvoice.Source.Should().Be(InvoiceSource.ISDOC);
    }

    [Fact]
    public async Task ImportFromParsedInvoiceAsync_DefaultsToInboundWhenNoCompanyVatConfigured()
    {
        var parsedInvoice = CreateTestParsedInvoice();
        var userId = Guid.NewGuid();
        Invoice? capturedInvoice = null;

        _configurationMock.Setup(c => c["Company:VatNo"]).Returns((string?)null);

        _parsedInvoiceProviderMock
            .Setup(x => x.GetByIdAsync(parsedInvoice.Id))
            .ReturnsAsync(parsedInvoice);

        _mainInvoiceProviderMock
            .Setup(x => x.CreateAsync(It.IsAny<Invoice>()))
            .Callback<Invoice>(i => capturedInvoice = i)
            .ReturnsAsync((Invoice i) => i);

        await _sut.ImportFromParsedInvoiceAsync(parsedInvoice.Id, userId);

        capturedInvoice.Should().NotBeNull();
        capturedInvoice!.Direction.Should().Be(InvoiceDirection.Inbound);
    }

    [Fact]
    public async Task ResyncFromFakturoidAsync_ThrowsWhenNotFakturoidSource()
    {
        var invoiceId = Guid.NewGuid();
        var invoice = new Invoice
        {
            Id = invoiceId,
            Source = InvoiceSource.Manual
        };

        _mainInvoiceProviderMock
            .Setup(x => x.GetWithDetailsAsync(invoiceId))
            .ReturnsAsync(invoice);

        var act = () => _sut.ResyncFromFakturoidAsync(invoiceId);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ResyncFromFakturoidAsync_UpdatesStatusFields()
    {
        var invoiceId = Guid.NewGuid();
        var fakturoidInvoiceId = Guid.NewGuid();
        var invoice = new Invoice
        {
            Id = invoiceId,
            Source = InvoiceSource.Fakturoid,
            FakturoidInvoiceId = fakturoidInvoiceId
        };
        var fakturoidInvoice = CreateTestFakturoidInvoice();
        fakturoidInvoice.Id = fakturoidInvoiceId;
        fakturoidInvoice.Paid = true;

        _mainInvoiceProviderMock
            .Setup(x => x.GetWithDetailsAsync(invoiceId))
            .ReturnsAsync(invoice);

        _fakturoidInvoiceProviderMock
            .Setup(x => x.GetByIdAsync(fakturoidInvoiceId))
            .ReturnsAsync(fakturoidInvoice);

        _mainInvoiceProviderMock
            .Setup(x => x.UpdateAsync(It.IsAny<Invoice>()))
            .Returns(Task.CompletedTask);

        await _sut.ResyncFromFakturoidAsync(invoiceId);

        _mainInvoiceProviderMock.Verify(x => x.UpdateAsync(It.IsAny<Invoice>()), Times.Once);
    }
}
