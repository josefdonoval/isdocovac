using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Isdocovac.Providers;

namespace Isdocovac.Services;

public interface IParsedInvoiceService
{
    Task<ParsedInvoice> UploadIsdocAsync(Guid userId, string fileName, long fileSize, string contentType, Stream fileContent);
    Task<IEnumerable<ParsedInvoice>> GetUserParsedInvoicesAsync(Guid userId, ParsedInvoiceStatus? status = null);
    Task<ParsedInvoice?> GetParsedInvoiceWithProcessingsAsync(Guid parsedInvoiceId);
    Task<ParsedInvoiceProcessing> StartParsingAsync(Guid parsedInvoiceId);
    Task UpdateParsedDataAsync(Guid parsedInvoiceId, ParsedInvoice updatedData);
    Task MarkReadyToImportAsync(Guid parsedInvoiceId);
    Task<string> GetDownloadUrlAsync(Guid parsedInvoiceId, int expirationMinutes = 60);
    Task DeleteAsync(Guid parsedInvoiceId);
}

public class ParsedInvoiceService : IParsedInvoiceService
{
    private readonly IParsedInvoiceProvider _parsedInvoiceProvider;
    private readonly IParsedInvoiceProcessingProvider _processingProvider;

    public ParsedInvoiceService(
        IParsedInvoiceProvider parsedInvoiceProvider,
        IParsedInvoiceProcessingProvider processingProvider)
    {
        _parsedInvoiceProvider = parsedInvoiceProvider;
        _processingProvider = processingProvider;
    }

    public async Task<ParsedInvoice> UploadIsdocAsync(Guid userId, string fileName, long fileSize, string contentType, Stream fileContent)
    {
        var parsedInvoice = await _parsedInvoiceProvider.CreateUploadAsync(userId, fileName, fileSize, contentType, fileContent);

        // Create initial processing attempt
        await _processingProvider.CreateProcessingAsync(parsedInvoice.Id, 1);

        return parsedInvoice;
    }

    public async Task<IEnumerable<ParsedInvoice>> GetUserParsedInvoicesAsync(Guid userId, ParsedInvoiceStatus? status = null)
    {
        return await _parsedInvoiceProvider.GetUserParsedInvoicesAsync(userId, status);
    }

    public async Task<ParsedInvoice?> GetParsedInvoiceWithProcessingsAsync(Guid parsedInvoiceId)
    {
        return await _parsedInvoiceProvider.GetWithProcessingsAsync(parsedInvoiceId);
    }

    public async Task<ParsedInvoiceProcessing> StartParsingAsync(Guid parsedInvoiceId)
    {
        var processings = await _processingProvider.GetProcessingsByParsedInvoiceIdAsync(parsedInvoiceId);
        var attemptNumber = processings.Any() ? processings.Max(p => p.AttemptNumber) + 1 : 1;

        var processing = await _processingProvider.CreateProcessingAsync(parsedInvoiceId, attemptNumber);
        await _parsedInvoiceProvider.UpdateStatusAsync(parsedInvoiceId, ParsedInvoiceStatus.Parsing);

        return processing;
    }

    public async Task UpdateParsedDataAsync(Guid parsedInvoiceId, ParsedInvoice updatedData)
    {
        await _parsedInvoiceProvider.UpdateParsedDataAsync(parsedInvoiceId, updatedData);
    }

    public async Task MarkReadyToImportAsync(Guid parsedInvoiceId)
    {
        await _parsedInvoiceProvider.UpdateStatusAsync(parsedInvoiceId, ParsedInvoiceStatus.ReadyToImport);
    }

    public async Task<string> GetDownloadUrlAsync(Guid parsedInvoiceId, int expirationMinutes = 60)
    {
        return await _parsedInvoiceProvider.GetSasUrlAsync(parsedInvoiceId, expirationMinutes);
    }

    public async Task DeleteAsync(Guid parsedInvoiceId)
    {
        await _parsedInvoiceProvider.DeleteAsync(parsedInvoiceId);
    }
}
