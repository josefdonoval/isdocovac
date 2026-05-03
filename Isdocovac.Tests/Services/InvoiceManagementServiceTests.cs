using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Isdocovac.Providers;
using Isdocovac.Services;

namespace Isdocovac.Tests.Services;

public class InvoiceManagementServiceTests
{
    private readonly Mock<IMainInvoiceProvider> _mainInvoiceProviderMock;
    private readonly Mock<IInvoiceAttachmentProvider> _attachmentProviderMock;
    private readonly Mock<IAzureBlobStorageProvider> _blobStorageProviderMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<InvoiceManagementService>> _loggerMock;
    private readonly InvoiceManagementService _sut;

    public InvoiceManagementServiceTests()
    {
        _mainInvoiceProviderMock = new Mock<IMainInvoiceProvider>();
        _attachmentProviderMock = new Mock<IInvoiceAttachmentProvider>();
        _blobStorageProviderMock = new Mock<IAzureBlobStorageProvider>();
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<InvoiceManagementService>>();

        _configurationMock.Setup(c => c["AzureStorage:InvoiceContainerName"])
            .Returns("invoice-uploads-test");

        _sut = new InvoiceManagementService(
            _mainInvoiceProviderMock.Object,
            _attachmentProviderMock.Object,
            _blobStorageProviderMock.Object,
            _configurationMock.Object,
            _loggerMock.Object);
    }

    private static Invoice CreateTestInvoice(Guid? userId = null)
    {
        return new Invoice
        {
            Id = Guid.NewGuid(),
            CompanyId = userId ?? Guid.NewGuid(),
            Direction = InvoiceDirection.Inbound,
            Number = "202603010001"
        };
    }

    private static Mock<IBrowserFile> CreateMockBrowserFile(
        string name = "invoice.pdf",
        long size = 1024,
        string contentType = "application/pdf")
    {
        var fileMock = new Mock<IBrowserFile>();
        fileMock.Setup(f => f.Name).Returns(name);
        fileMock.Setup(f => f.Size).Returns(size);
        fileMock.Setup(f => f.ContentType).Returns(contentType);
        fileMock.Setup(f => f.OpenReadStream(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(new MemoryStream(new byte[size]));
        return fileMock;
    }

    [Fact]
    public async Task GetCompanyInvoicesAsync_DelegatesToProvider()
    {
        var userId = Guid.NewGuid();
        var expectedInvoices = new List<Invoice> { CreateTestInvoice(userId) };

        _mainInvoiceProviderMock
            .Setup(x => x.GetByCompanyIdAsync(userId, null, 1, 20))
            .ReturnsAsync(expectedInvoices);

        var result = await _sut.GetCompanyInvoicesAsync(userId, null, 1, 20);

        result.Should().BeEquivalentTo(expectedInvoices);
        _mainInvoiceProviderMock.Verify(x => x.GetByCompanyIdAsync(userId, null, 1, 20), Times.Once);
    }

    [Fact]
    public async Task GenerateInvoiceNumberAsync_FormatsCorrectly()
    {
        var userId = Guid.NewGuid();
        var vatDate = new DateTime(2026, 3, 1);

        _mainInvoiceProviderMock
            .Setup(x => x.GetCountForVatMonthAsync(userId, 2026, 3))
            .ReturnsAsync(0);

        var result = await _sut.GenerateInvoiceNumberAsync(userId, vatDate);

        result.Should().Be("2026030001");
    }

    [Fact]
    public async Task GenerateInvoiceNumberAsync_IncrementsCounter()
    {
        var userId = Guid.NewGuid();
        var vatDate = new DateTime(2026, 3, 1);

        _mainInvoiceProviderMock
            .Setup(x => x.GetCountForVatMonthAsync(userId, 2026, 3))
            .ReturnsAsync(5);

        var result = await _sut.GenerateInvoiceNumberAsync(userId, vatDate);

        result.Should().Be("2026030006");
    }

    [Fact]
    public async Task CreateManualInvoiceAsync_SetsSourceToManual()
    {
        var invoice = CreateTestInvoice();
        Invoice? capturedInvoice = null;

        _mainInvoiceProviderMock
            .Setup(x => x.CreateAsync(It.IsAny<Invoice>()))
            .Callback<Invoice>(i => capturedInvoice = i)
            .ReturnsAsync((Invoice i) => i);

        await _sut.CreateManualInvoiceAsync(invoice);

        capturedInvoice.Should().NotBeNull();
        capturedInvoice!.Source.Should().Be(InvoiceSource.Manual);
    }

    [Fact]
    public async Task CreateManualInvoiceWithAttachmentAsync_CreatesWithoutAttachmentWhenNull()
    {
        var invoice = CreateTestInvoice();

        _mainInvoiceProviderMock
            .Setup(x => x.CreateAsync(It.IsAny<Invoice>()))
            .ReturnsAsync((Invoice i) => i);

        await _sut.CreateManualInvoiceWithAttachmentAsync(invoice, null);

        _mainInvoiceProviderMock.Verify(x => x.CreateAsync(It.IsAny<Invoice>()), Times.Once);
        _blobStorageProviderMock.Verify(
            x => x.UploadBlobAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateManualInvoiceWithAttachmentAsync_UploadsPdf()
    {
        var invoice = CreateTestInvoice();
        var fileMock = CreateMockBrowserFile();

        _mainInvoiceProviderMock
            .Setup(x => x.CreateAsync(It.IsAny<Invoice>()))
            .ReturnsAsync((Invoice i) => i);

        _blobStorageProviderMock
            .Setup(x => x.UploadBlobAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync("https://blob.test/invoice.pdf");

        _attachmentProviderMock
            .Setup(x => x.CreateAsync(It.IsAny<InvoiceAttachment>()))
            .ReturnsAsync((InvoiceAttachment a) => a);

        await _sut.CreateManualInvoiceWithAttachmentAsync(invoice, fileMock.Object);

        _blobStorageProviderMock.Verify(
            x => x.UploadBlobAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), "application/pdf"),
            Times.Once);
        _attachmentProviderMock.Verify(x => x.CreateAsync(It.IsAny<InvoiceAttachment>()), Times.Once);
    }

    [Fact]
    public async Task UpdateInvoiceWithAttachmentAsync_ReplacesOldAttachment()
    {
        var invoice = CreateTestInvoice();
        var existingAttachmentId = Guid.NewGuid();
        var existingAttachment = new InvoiceAttachment
        {
            Id = existingAttachmentId,
            InvoiceId = invoice.Id,
            BlobContainerName = "old-container",
            BlobName = "old-blob",
            Filename = "old.pdf"
        };
        var newFileMock = CreateMockBrowserFile();

        _attachmentProviderMock
            .Setup(x => x.GetByIdAsync(existingAttachmentId))
            .ReturnsAsync(existingAttachment);

        _mainInvoiceProviderMock
            .Setup(x => x.UpdateAsync(It.IsAny<Invoice>()))
            .Returns(Task.CompletedTask);

        _blobStorageProviderMock
            .Setup(x => x.DeleteBlobAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _blobStorageProviderMock
            .Setup(x => x.UploadBlobAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync("https://blob.test/new-invoice.pdf");

        _attachmentProviderMock
            .Setup(x => x.CreateAsync(It.IsAny<InvoiceAttachment>()))
            .ReturnsAsync((InvoiceAttachment a) => a);

        await _sut.UpdateInvoiceWithAttachmentAsync(invoice, newFileMock.Object, existingAttachmentId);

        _blobStorageProviderMock.Verify(
            x => x.DeleteBlobAsync("old-container", "old-blob"), Times.Once);
        _attachmentProviderMock.Verify(x => x.DeleteAsync(existingAttachmentId), Times.Once);
        _blobStorageProviderMock.Verify(
            x => x.UploadBlobAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteInvoiceAsync_DelegatesToProvider()
    {
        var invoiceId = Guid.NewGuid();

        _mainInvoiceProviderMock
            .Setup(x => x.DeleteAsync(invoiceId))
            .Returns(Task.CompletedTask);

        await _sut.DeleteInvoiceAsync(invoiceId);

        _mainInvoiceProviderMock.Verify(x => x.DeleteAsync(invoiceId), Times.Once);
    }

    [Fact]
    public async Task UploadPdfAttachment_RejectsFileOver10MB()
    {
        var invoice = CreateTestInvoice();
        var largeSizeBytes = 11L * 1024 * 1024; // 11 MB
        var fileMock = CreateMockBrowserFile(size: largeSizeBytes);

        _mainInvoiceProviderMock
            .Setup(x => x.CreateAsync(It.IsAny<Invoice>()))
            .ReturnsAsync((Invoice i) => i);

        var act = () => _sut.CreateManualInvoiceWithAttachmentAsync(invoice, fileMock.Object);

        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task UploadPdfAttachment_RejectsNonPdfFile()
    {
        var invoice = CreateTestInvoice();
        var fileMock = CreateMockBrowserFile(name: "document.docx", contentType: "application/msword");

        _mainInvoiceProviderMock
            .Setup(x => x.CreateAsync(It.IsAny<Invoice>()))
            .ReturnsAsync((Invoice i) => i);

        var act = () => _sut.CreateManualInvoiceWithAttachmentAsync(invoice, fileMock.Object);

        await act.Should().ThrowAsync<Exception>();
    }
}
