using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Isdocovac.Providers;
using Isdocovac.Services.ISDOC;

namespace Isdocovac.Services;

public interface IParsedInvoiceService
{
    Task<ParsedInvoice> UploadIsdocAsync(Guid companyId, string fileName, long fileSize, string contentType, Stream fileContent, InvoiceLineMode? lineMode = null);

    /// <summary>
    /// Creates a ParsedInvoice from an already-uploaded blob (e.g. an email attachment) and
    /// kicks off the appropriate parser pipeline (PDF or XML/ISDOC). Caller must have an explicit
    /// user-confirmed intent to import — this method may invoke paid AI extraction services.
    /// </summary>
    Task<ParsedInvoice> IngestFromExistingBlobAsync(
        Guid companyId,
        string blobContainerName,
        string blobName,
        string fileName,
        long fileSize,
        string contentType,
        InvoiceLineMode? lineMode = null);

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
        await StartParsingPipelineAsync(parsedInvoice.Id, fileName, contentType, lineMode);
        return parsedInvoice;
    }

    public async Task<ParsedInvoice> IngestFromExistingBlobAsync(
        Guid companyId,
        string blobContainerName,
        string blobName,
        string fileName,
        long fileSize,
        string contentType,
        InvoiceLineMode? lineMode = null)
    {
        var parsedInvoice = await _parsedInvoiceProvider.CreateFromExistingBlobAsync(
            companyId, blobContainerName, blobName, fileName, fileSize, contentType);
        await StartParsingPipelineAsync(parsedInvoice.Id, fileName, contentType, lineMode);
        return parsedInvoice;
    }

    /// <summary>
    /// Shared post-creation hook: tags source file type, optionally records line mode for PDFs,
    /// creates the first processing attempt and starts the appropriate parser on a background task.
    /// </summary>
    private async Task StartParsingPipelineAsync(Guid parsedInvoiceId, string fileName, string contentType, InvoiceLineMode? lineMode)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var isPdf = contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) || ext == ".pdf";
        var isImage = !isPdf && (
            contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
            ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".heic" or ".tif" or ".tiff");

        var sourceFileType = isPdf ? "PDF" : isImage ? "Image" : "XML";
        await _parsedInvoiceProvider.UpdateSourceFileTypeAsync(parsedInvoiceId, sourceFileType);

        if (isPdf && lineMode.HasValue)
        {
            await _parsedInvoiceProvider.UpdateLineModeAsync(parsedInvoiceId, lineMode.Value);
        }

        var processing = await _processingProvider.CreateProcessingAsync(parsedInvoiceId, 1);
        var processingId = processing.Id;

        if (isPdf)
        {
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
        else if (isImage)
        {
            // Image extraction (Claude/OpenAI vision) is not implemented yet. Mark the parsed
            // invoice as awaiting manual entry so the user can fill the form and import it.
            // The original image stays in blob storage and is shown as preview on the import page.
            await _parsedInvoiceProvider.UpdateStatusAsync(parsedInvoiceId, ParsedInvoiceStatus.Parsed);
            await _processingProvider.UpdateProcessingStatusAsync(processingId, ProcessingStatus.Completed);
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
