using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Isdocovac.Providers;

namespace Isdocovac.Services;

public interface IInvoiceService
{
    Task<InvoiceUpload> UploadInvoiceAsync(Guid userId, string fileName, long fileSize, string contentType, Stream fileContent);
    Task<IEnumerable<InvoiceUpload>> GetUserInvoicesAsync(Guid userId);
    Task<InvoiceUpload?> GetInvoiceWithProcessingsAsync(Guid invoiceId);
    Task<InvoiceProcessing> StartProcessingAsync(Guid invoiceId);
    Task<string> GetInvoiceDownloadUrlAsync(Guid invoiceId, int expirationMinutes = 60);
    Task DeleteInvoiceAsync(Guid invoiceId);
}

public class InvoiceService : IInvoiceService
{
    private readonly IInvoiceProvider _invoiceProvider;
    private readonly IInvoiceProcessingProvider _processingProvider;

    public InvoiceService(IInvoiceProvider invoiceProvider, IInvoiceProcessingProvider processingProvider)
    {
        _invoiceProvider = invoiceProvider;
        _processingProvider = processingProvider;
    }

    public async Task<InvoiceUpload> UploadInvoiceAsync(Guid userId, string fileName, long fileSize, string contentType, Stream fileContent)
    {
        var upload = await _invoiceProvider.CreateUploadAsync(userId, fileName, fileSize, contentType, fileContent);

        // Create initial processing attempt
        await _processingProvider.CreateProcessingAsync(upload.Id, 1);

        return upload;
    }

    public async Task<IEnumerable<InvoiceUpload>> GetUserInvoicesAsync(Guid userId)
    {
        return await _invoiceProvider.GetUserUploadsAsync(userId);
    }

    public async Task<InvoiceUpload?> GetInvoiceWithProcessingsAsync(Guid invoiceId)
    {
        return await _invoiceProvider.GetUploadWithParsedDataAsync(invoiceId);
    }

    public async Task<InvoiceProcessing> StartProcessingAsync(Guid invoiceId)
    {
        var processings = await _processingProvider.GetProcessingsByInvoiceIdAsync(invoiceId);
        var attemptNumber = processings.Any() ? processings.Max(p => p.AttemptNumber) + 1 : 1;

        var processing = await _processingProvider.CreateProcessingAsync(invoiceId, attemptNumber);
        await _invoiceProvider.UpdateUploadStatusAsync(invoiceId, InvoiceUploadStatus.Processing);

        return processing;
    }

    public async Task<string> GetInvoiceDownloadUrlAsync(Guid invoiceId, int expirationMinutes = 60)
    {
        return await _invoiceProvider.GetUploadSasUrlAsync(invoiceId, expirationMinutes);
    }

    public async Task DeleteInvoiceAsync(Guid invoiceId)
    {
        await _invoiceProvider.DeleteUploadAsync(invoiceId);
    }
}
