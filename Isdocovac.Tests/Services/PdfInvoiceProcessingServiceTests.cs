using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Isdocovac.Models.Extraction;
using Isdocovac.Providers;
using Isdocovac.Services;
using Isdocovac.Services.ISDOC;
using Isdocovac.Services.Claude;

namespace Isdocovac.Tests.Services;

public class PdfInvoiceProcessingServiceTests
{
    private readonly Mock<IParsedInvoiceProvider> _parsedInvoiceProviderMock;
    private readonly Mock<IParsedInvoiceProcessingProvider> _processingProviderMock;
    private readonly Mock<IAzureBlobStorageProvider> _blobStorageProviderMock;
    private readonly Mock<IInvoiceParsingService> _invoiceParsingServiceMock;
    private readonly Mock<IIsdocGeneratorService> _isdocGeneratorServiceMock;
    private readonly Mock<ILogger<PdfInvoiceProcessingService>> _loggerMock;
    private readonly PdfInvoiceProcessingService _sut;

    public PdfInvoiceProcessingServiceTests()
    {
        _parsedInvoiceProviderMock = new Mock<IParsedInvoiceProvider>();
        _processingProviderMock = new Mock<IParsedInvoiceProcessingProvider>();
        _blobStorageProviderMock = new Mock<IAzureBlobStorageProvider>();
        _invoiceParsingServiceMock = new Mock<IInvoiceParsingService>();
        _isdocGeneratorServiceMock = new Mock<IIsdocGeneratorService>();
        _loggerMock = new Mock<ILogger<PdfInvoiceProcessingService>>();

        _sut = new PdfInvoiceProcessingService(
            _parsedInvoiceProviderMock.Object,
            _processingProviderMock.Object,
            _blobStorageProviderMock.Object,
            _invoiceParsingServiceMock.Object,
            _isdocGeneratorServiceMock.Object,
            _loggerMock.Object);
    }

    private static ParsedInvoice CreateTestParsedInvoice()
    {
        return new ParsedInvoice
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            FileName = "test-invoice.pdf",
            BlobContainerName = "test-container",
            BlobName = "test-blob",
            Status = ParsedInvoiceStatus.Uploaded,
            LineMode = InvoiceLineMode.Detailed
        };
    }

    private static InvoiceExtractionResult CreateSuccessfulExtraction()
    {
        return new InvoiceExtractionResult
        {
            Success = true,
            InvoiceNumber = "FV-001",
            Lines = new List<InvoiceLineItem>(),
            VatRates = new List<VatRateSummary>(),
            Subtotal = 1000m,
            Total = 1210m
        };
    }

    private void SetupHappyPath(ParsedInvoice parsedInvoice)
    {
        _parsedInvoiceProviderMock
            .Setup(x => x.GetByIdAsync(parsedInvoice.Id))
            .ReturnsAsync(parsedInvoice);

        _blobStorageProviderMock
            .Setup(x => x.DownloadBlobAsync(parsedInvoice.BlobContainerName, parsedInvoice.BlobName))
            .ReturnsAsync(new MemoryStream(new byte[100]));

        _invoiceParsingServiceMock
            .Setup(x => x.UploadPdfAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync("file-id-123");

        _invoiceParsingServiceMock
            .Setup(x => x.ExtractInvoiceDataAsync("file-id-123", InvoiceLineMode.Detailed))
            .ReturnsAsync(CreateSuccessfulExtraction());

        _isdocGeneratorServiceMock
            .Setup(x => x.GenerateIsdocXmlAsync(It.IsAny<InvoiceExtractionResult>()))
            .ReturnsAsync("<Invoice/>");

        _blobStorageProviderMock
            .Setup(x => x.GetBlobUrl(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("https://blob.test/generated.xml");
    }

    [Fact]
    public async Task ProcessPdfInvoiceAsync_CompletesSuccessfullyWithAllSteps()
    {
        var parsedInvoice = CreateTestParsedInvoice();
        var processingId = Guid.NewGuid();
        SetupHappyPath(parsedInvoice);

        await _sut.ProcessPdfInvoiceAsync(parsedInvoice.Id, processingId);

        _processingProviderMock.Verify(
            x => x.CompleteProcessingAsync(processingId), Times.Once);
        _parsedInvoiceProviderMock.Verify(
            x => x.UpdateStatusAsync(parsedInvoice.Id, ParsedInvoiceStatus.Parsed), Times.Once);
    }

    [Fact]
    public async Task ProcessPdfInvoiceAsync_FailsWhenParsedInvoiceNotFound()
    {
        var parsedInvoiceId = Guid.NewGuid();
        var processingId = Guid.NewGuid();

        _parsedInvoiceProviderMock
            .Setup(x => x.GetByIdAsync(parsedInvoiceId))
            .ReturnsAsync((ParsedInvoice?)null);

        var act = () => _sut.ProcessPdfInvoiceAsync(parsedInvoiceId, processingId);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _processingProviderMock.Verify(
            x => x.FailProcessingAsync(processingId, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPdfInvoiceAsync_FailsWhenLineModeIsNull()
    {
        var parsedInvoice = CreateTestParsedInvoice();
        parsedInvoice.LineMode = null;
        var processingId = Guid.NewGuid();

        _parsedInvoiceProviderMock
            .Setup(x => x.GetByIdAsync(parsedInvoice.Id))
            .ReturnsAsync(parsedInvoice);

        var act = () => _sut.ProcessPdfInvoiceAsync(parsedInvoice.Id, processingId);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _processingProviderMock.Verify(
            x => x.FailProcessingAsync(processingId, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPdfInvoiceAsync_HandlesUploadStepFailure()
    {
        var parsedInvoice = CreateTestParsedInvoice();
        var processingId = Guid.NewGuid();

        _parsedInvoiceProviderMock
            .Setup(x => x.GetByIdAsync(parsedInvoice.Id))
            .ReturnsAsync(parsedInvoice);

        _blobStorageProviderMock
            .Setup(x => x.DownloadBlobAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new MemoryStream(new byte[100]));

        _invoiceParsingServiceMock
            .Setup(x => x.UploadPdfAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("Upload failed"));

        var act = () => _sut.ProcessPdfInvoiceAsync(parsedInvoice.Id, processingId);

        await act.Should().ThrowAsync<HttpRequestException>();
        _processingProviderMock.Verify(
            x => x.FailProcessingAsync(processingId, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPdfInvoiceAsync_CallsCleanupInFinallyBlock()
    {
        var parsedInvoice = CreateTestParsedInvoice();
        var processingId = Guid.NewGuid();
        SetupHappyPath(parsedInvoice);

        await _sut.ProcessPdfInvoiceAsync(parsedInvoice.Id, processingId);

        _invoiceParsingServiceMock.Verify(
            x => x.DeleteFileAsync("file-id-123"), Times.Once);
    }

    [Fact]
    public async Task ProcessPdfInvoiceAsync_RecordsStepErrors()
    {
        var parsedInvoice = CreateTestParsedInvoice();
        var processingId = Guid.NewGuid();

        _parsedInvoiceProviderMock
            .Setup(x => x.GetByIdAsync(parsedInvoice.Id))
            .ReturnsAsync(parsedInvoice);

        _blobStorageProviderMock
            .Setup(x => x.DownloadBlobAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Blob download failed"));

        var act = () => _sut.ProcessPdfInvoiceAsync(parsedInvoice.Id, processingId);

        await act.Should().ThrowAsync<Exception>();
        _processingProviderMock.Verify(
            x => x.UpdateStepErrorsAsync(processingId, It.IsAny<Dictionary<string, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RetryProcessingAsync_IncrementsAttemptNumber()
    {
        var parsedInvoice = CreateTestParsedInvoice();
        var existingProcessings = new List<ParsedInvoiceProcessing>
        {
            new ParsedInvoiceProcessing { AttemptNumber = 1 },
            new ParsedInvoiceProcessing { AttemptNumber = 2 }
        };
        int capturedAttemptNumber = 0;

        _parsedInvoiceProviderMock
            .Setup(x => x.GetByIdAsync(parsedInvoice.Id))
            .ReturnsAsync(parsedInvoice);

        _processingProviderMock
            .Setup(x => x.GetProcessingsByParsedInvoiceIdAsync(parsedInvoice.Id))
            .ReturnsAsync(existingProcessings);

        _processingProviderMock
            .Setup(x => x.CreateProcessingAsync(It.IsAny<Guid>(), It.IsAny<int>()))
            .Callback<Guid, int>((_, attempt) => capturedAttemptNumber = attempt)
            .ReturnsAsync((Guid id, int attempt) => new ParsedInvoiceProcessing { Id = Guid.NewGuid(), ParsedInvoiceId = id, AttemptNumber = attempt });

        _parsedInvoiceProviderMock
            .Setup(x => x.UpdateStatusAsync(parsedInvoice.Id, ParsedInvoiceStatus.Uploaded))
            .Returns(Task.CompletedTask);

        // Setup remaining happy path for ProcessPdfInvoiceAsync call
        _blobStorageProviderMock
            .Setup(x => x.DownloadBlobAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new MemoryStream(new byte[100]));

        _invoiceParsingServiceMock
            .Setup(x => x.UploadPdfAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync("file-id-456");

        _invoiceParsingServiceMock
            .Setup(x => x.ExtractInvoiceDataAsync(It.IsAny<string>(), It.IsAny<InvoiceLineMode>()))
            .ReturnsAsync(CreateSuccessfulExtraction());

        _isdocGeneratorServiceMock
            .Setup(x => x.GenerateIsdocXmlAsync(It.IsAny<InvoiceExtractionResult>()))
            .ReturnsAsync("<Invoice/>");

        await _sut.RetryProcessingAsync(parsedInvoice.Id);

        capturedAttemptNumber.Should().Be(3);
    }
}
