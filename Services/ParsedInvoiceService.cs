using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Isdocovac.Providers;
using Isdocovac.Services.ISDOC;

namespace Isdocovac.Services;

public interface IParsedInvoiceService
{
    Task<ParsedInvoice> UploadIsdocAsync(Guid companyId, string fileName, long fileSize, string contentType, Stream fileContent, InvoiceLineMode? lineMode = null);
    Task<IEnumerable<ParsedInvoice>> GetCompanyParsedInvoicesAsync(Guid companyId, ParsedInvoiceStatus? status = null);
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
    private readonly IMainInvoiceProvider _invoiceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ParsedInvoiceService> _logger;

    public ParsedInvoiceService(
        IParsedInvoiceProvider parsedInvoiceProvider,
        IParsedInvoiceProcessingProvider processingProvider,
        IMainInvoiceProvider invoiceProvider,
        IServiceScopeFactory scopeFactory,
        ILogger<ParsedInvoiceService> logger)
    {
        _parsedInvoiceProvider = parsedInvoiceProvider;
        _processingProvider = processingProvider;
        _invoiceProvider = invoiceProvider;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<ParsedInvoice> UploadIsdocAsync(Guid companyId, string fileName, long fileSize, string contentType, Stream fileContent, InvoiceLineMode? lineMode = null)
    {
        var parsedInvoice = await _parsedInvoiceProvider.CreateUploadAsync(companyId, fileName, fileSize, contentType, fileContent);

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

        var parsedInvoiceId = parsedInvoice.Id;
        var processingId = processing.Id;

        if (isPdf)
        {
            // Background processing runs in its own DI scope so the DbContext outlives the request scope.
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                try
                {
                    var pdfService = scope.ServiceProvider.GetRequiredService<IPdfInvoiceProcessingService>();
                    await pdfService.ProcessPdfInvoiceAsync(parsedInvoiceId, processingId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background PDF processing failed for {ParsedInvoiceId}", parsedInvoiceId);
                }
            });
        }
        else
        {
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                try
                {
                    var xmlService = scope.ServiceProvider.GetRequiredService<IIsdocXmlParsingService>();
                    await xmlService.ProcessIsdocXmlAsync(parsedInvoiceId, processingId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background ISDOC XML processing failed for {ParsedInvoiceId}", parsedInvoiceId);
                }
            });
        }

        return parsedInvoice;
    }

    public async Task<IEnumerable<ParsedInvoice>> GetCompanyParsedInvoicesAsync(Guid companyId, ParsedInvoiceStatus? status = null)
    {
        return await _parsedInvoiceProvider.GetCompanyParsedInvoicesAsync(companyId, status);
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
        var parsedInvoice = await _parsedInvoiceProvider.GetByIdAsync(parsedInvoiceId);
        if (parsedInvoice == null)
            return;

        // Soft-delete the imported invoice too if it exists
        if (parsedInvoice.ImportedInvoiceId.HasValue)
        {
            await _invoiceProvider.DeleteAsync(parsedInvoice.ImportedInvoiceId.Value);
        }

        await _parsedInvoiceProvider.DeleteAsync(parsedInvoiceId);
    }
}
