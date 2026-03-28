using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Isdocovac.Providers;
using Isdocovac.Services.ISDOC;

namespace Isdocovac.Services;

public interface IParsedInvoiceService
{
    Task<ParsedInvoice> UploadIsdocAsync(Guid userId, string fileName, long fileSize, string contentType, Stream fileContent, InvoiceLineMode? lineMode = null);
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
    private readonly IPdfInvoiceProcessingService? _pdfProcessingService;
    private readonly IIsdocXmlParsingService? _isdocXmlParsingService;

    public ParsedInvoiceService(
        IParsedInvoiceProvider parsedInvoiceProvider,
        IParsedInvoiceProcessingProvider processingProvider,
        IPdfInvoiceProcessingService? pdfProcessingService = null,
        IIsdocXmlParsingService? isdocXmlParsingService = null)
    {
        _parsedInvoiceProvider = parsedInvoiceProvider;
        _processingProvider = processingProvider;
        _pdfProcessingService = pdfProcessingService;
        _isdocXmlParsingService = isdocXmlParsingService;
    }

    public async Task<ParsedInvoice> UploadIsdocAsync(Guid userId, string fileName, long fileSize, string contentType, Stream fileContent, InvoiceLineMode? lineMode = null)
    {
        var parsedInvoice = await _parsedInvoiceProvider.CreateUploadAsync(userId, fileName, fileSize, contentType, fileContent);

        // Detect file type
        var isPdf = contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
        var sourceFileType = isPdf ? "PDF" : "XML";

        await _parsedInvoiceProvider.UpdateSourceFileTypeAsync(parsedInvoice.Id, sourceFileType);

        // Set line mode for PDFs
        if (isPdf && lineMode.HasValue)
        {
            await _parsedInvoiceProvider.UpdateLineModeAsync(parsedInvoice.Id, lineMode.Value);
        }

        // Create initial processing attempt
        var processing = await _processingProvider.CreateProcessingAsync(parsedInvoice.Id, 1);

        if (isPdf && _pdfProcessingService != null)
        {
            // Process PDF asynchronously (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _pdfProcessingService.ProcessPdfInvoiceAsync(parsedInvoice.Id, processing.Id);
                }
                catch (Exception)
                {
                    // Errors are already logged in PdfInvoiceProcessingService
                }
            });
        }
        else if (!isPdf && _isdocXmlParsingService != null)
        {
            // Process ISDOC XML asynchronously (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _isdocXmlParsingService.ProcessIsdocXmlAsync(parsedInvoice.Id, processing.Id);
                }
                catch (Exception)
                {
                    // Errors are already logged in IsdocXmlParsingService
                }
            });
        }

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
