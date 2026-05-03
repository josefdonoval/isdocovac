using FluentAssertions;
using Moq;
using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Isdocovac.Providers;
using Isdocovac.Services;
using Isdocovac.Services.ISDOC;

namespace Isdocovac.Tests.Services;

public class ParsedInvoiceServiceTests
{
    private readonly Mock<IParsedInvoiceProvider> _parsedInvoiceProviderMock;
    private readonly Mock<IParsedInvoiceProcessingProvider> _processingProviderMock;
    private readonly Mock<IMainInvoiceProvider> _mainInvoiceProviderMock;
    private readonly Mock<IPdfInvoiceProcessingService> _pdfProcessingServiceMock;
    private readonly Mock<IIsdocXmlParsingService> _isdocXmlParsingServiceMock;
    private readonly ParsedInvoiceService _sut;

    public ParsedInvoiceServiceTests()
    {
        _parsedInvoiceProviderMock = new Mock<IParsedInvoiceProvider>();
        _processingProviderMock = new Mock<IParsedInvoiceProcessingProvider>();
        _mainInvoiceProviderMock = new Mock<IMainInvoiceProvider>();
        _pdfProcessingServiceMock = new Mock<IPdfInvoiceProcessingService>();
        _isdocXmlParsingServiceMock = new Mock<IIsdocXmlParsingService>();

        _sut = new ParsedInvoiceService(
            _parsedInvoiceProviderMock.Object,
            _processingProviderMock.Object,
            _mainInvoiceProviderMock.Object,
            _pdfProcessingServiceMock.Object,
            _isdocXmlParsingServiceMock.Object);
    }

    private static ParsedInvoice CreateTestParsedInvoice(
        ParsedInvoiceStatus status = ParsedInvoiceStatus.Uploaded,
        Guid? importedInvoiceId = null)
    {
        return new ParsedInvoice
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            FileName = "test.isdoc",
            Status = status,
            ImportedInvoiceId = importedInvoiceId
        };
    }

    private void SetupUploadMocks(ParsedInvoice returnedInvoice)
    {
        _parsedInvoiceProviderMock
            .Setup(x => x.CreateUploadAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<Stream>()))
            .ReturnsAsync(returnedInvoice);

        _processingProviderMock
            .Setup(x => x.CreateProcessingAsync(It.IsAny<Guid>(), It.IsAny<int>()))
            .ReturnsAsync((Guid id, int attempt) => new ParsedInvoiceProcessing { Id = Guid.NewGuid(), ParsedInvoiceId = id, AttemptNumber = attempt });
    }

    [Fact]
    public async Task UploadIsdocAsync_DetectsPdfFromContentType()
    {
        var userId = Guid.NewGuid();
        var fileContent = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });
        var returnedInvoice = CreateTestParsedInvoice();
        SetupUploadMocks(returnedInvoice);

        var result = await _sut.UploadIsdocAsync(
            userId, "invoice.pdf", 1024, "application/pdf", fileContent, null);

        result.Should().NotBeNull();
        _parsedInvoiceProviderMock.Verify(
            x => x.UpdateSourceFileTypeAsync(returnedInvoice.Id, "PDF"), Times.Once);
    }

    [Fact]
    public async Task UploadIsdocAsync_DetectsXmlFromExtension()
    {
        var userId = Guid.NewGuid();
        var fileContent = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("<Invoice/>"));
        var returnedInvoice = CreateTestParsedInvoice();
        SetupUploadMocks(returnedInvoice);

        var result = await _sut.UploadIsdocAsync(
            userId, "invoice.isdoc", 512, "application/xml", fileContent, null);

        result.Should().NotBeNull();
        _parsedInvoiceProviderMock.Verify(
            x => x.UpdateSourceFileTypeAsync(returnedInvoice.Id, "XML"), Times.Once);
    }

    [Fact]
    public async Task UploadIsdocAsync_SetsLineModeForPdf()
    {
        var userId = Guid.NewGuid();
        var fileContent = new MemoryStream(new byte[100]);
        var lineMode = InvoiceLineMode.Detailed;
        var returnedInvoice = CreateTestParsedInvoice();
        SetupUploadMocks(returnedInvoice);

        await _sut.UploadIsdocAsync(
            userId, "invoice.pdf", 1024, "application/pdf", fileContent, lineMode);

        _parsedInvoiceProviderMock.Verify(
            x => x.UpdateLineModeAsync(returnedInvoice.Id, lineMode), Times.Once);
    }

    [Fact]
    public async Task UploadIsdocAsync_DoesNotSetLineModeForXml()
    {
        var userId = Guid.NewGuid();
        var fileContent = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("<Invoice/>"));
        var returnedInvoice = CreateTestParsedInvoice();
        SetupUploadMocks(returnedInvoice);

        await _sut.UploadIsdocAsync(
            userId, "invoice.isdoc", 512, "application/xml", fileContent, InvoiceLineMode.Detailed);

        _parsedInvoiceProviderMock.Verify(
            x => x.UpdateLineModeAsync(It.IsAny<Guid>(), It.IsAny<InvoiceLineMode>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_DoesNothingWhenNotFound()
    {
        var parsedInvoiceId = Guid.NewGuid();

        _parsedInvoiceProviderMock
            .Setup(x => x.GetByIdAsync(parsedInvoiceId))
            .ReturnsAsync((ParsedInvoice?)null);

        await _sut.DeleteAsync(parsedInvoiceId);

        _parsedInvoiceProviderMock.Verify(
            x => x.DeleteAsync(It.IsAny<Guid>()), Times.Never);
        _mainInvoiceProviderMock.Verify(
            x => x.DeleteAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_DeletesImportedInvoiceWhenExists()
    {
        var importedInvoiceId = Guid.NewGuid();
        var parsedInvoice = CreateTestParsedInvoice(importedInvoiceId: importedInvoiceId);

        _parsedInvoiceProviderMock
            .Setup(x => x.GetByIdAsync(parsedInvoice.Id))
            .ReturnsAsync(parsedInvoice);

        _mainInvoiceProviderMock
            .Setup(x => x.DeleteAsync(importedInvoiceId))
            .Returns(Task.CompletedTask);

        _parsedInvoiceProviderMock
            .Setup(x => x.DeleteAsync(parsedInvoice.Id))
            .Returns(Task.CompletedTask);

        await _sut.DeleteAsync(parsedInvoice.Id);

        _mainInvoiceProviderMock.Verify(x => x.DeleteAsync(importedInvoiceId), Times.Once);
        _parsedInvoiceProviderMock.Verify(x => x.DeleteAsync(parsedInvoice.Id), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_DeletesParsedInvoice()
    {
        var parsedInvoice = CreateTestParsedInvoice();

        _parsedInvoiceProviderMock
            .Setup(x => x.GetByIdAsync(parsedInvoice.Id))
            .ReturnsAsync(parsedInvoice);

        _parsedInvoiceProviderMock
            .Setup(x => x.DeleteAsync(parsedInvoice.Id))
            .Returns(Task.CompletedTask);

        await _sut.DeleteAsync(parsedInvoice.Id);

        _parsedInvoiceProviderMock.Verify(x => x.DeleteAsync(parsedInvoice.Id), Times.Once);
    }

    [Fact]
    public async Task StartParsingAsync_IncrementsAttemptNumber()
    {
        var parsedInvoiceId = Guid.NewGuid();
        var existingProcessings = new List<ParsedInvoiceProcessing>
        {
            new ParsedInvoiceProcessing { AttemptNumber = 1 },
            new ParsedInvoiceProcessing { AttemptNumber = 2 }
        };
        int capturedAttemptNumber = 0;

        _processingProviderMock
            .Setup(x => x.GetProcessingsByParsedInvoiceIdAsync(parsedInvoiceId))
            .ReturnsAsync(existingProcessings);

        _processingProviderMock
            .Setup(x => x.CreateProcessingAsync(It.IsAny<Guid>(), It.IsAny<int>()))
            .Callback<Guid, int>((_, attempt) => capturedAttemptNumber = attempt)
            .ReturnsAsync((Guid id, int attempt) => new ParsedInvoiceProcessing { Id = Guid.NewGuid(), ParsedInvoiceId = id, AttemptNumber = attempt });

        _parsedInvoiceProviderMock
            .Setup(x => x.UpdateStatusAsync(parsedInvoiceId, ParsedInvoiceStatus.Parsing))
            .Returns(Task.CompletedTask);

        await _sut.StartParsingAsync(parsedInvoiceId);

        capturedAttemptNumber.Should().Be(3);
    }
}
