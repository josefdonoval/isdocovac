using System.Text.Json;
using Isdocovac.Models.Enums;
using Isdocovac.Providers;
using Isdocovac.Services.Claude;
using Isdocovac.Services.ISDOC;

namespace Isdocovac.Services;

public interface IPdfInvoiceProcessingService
{
    Task ProcessPdfInvoiceAsync(Guid parsedInvoiceId, Guid processingId);
    Task RetryProcessingAsync(Guid parsedInvoiceId);
}

public class PdfInvoiceProcessingService : IPdfInvoiceProcessingService
{
    private readonly IParsedInvoiceProvider _parsedInvoiceProvider;
    private readonly IParsedInvoiceProcessingProvider _processingProvider;
    private readonly IAzureBlobStorageProvider _blobStorageProvider;
    private readonly IInvoiceParsingService _invoiceParsingService;
    private readonly IIsdocGeneratorService _isdocGenerator;
    private readonly IIsdocValidationService _isdocValidator;
    private readonly ILogger<PdfInvoiceProcessingService> _logger;

    public PdfInvoiceProcessingService(
        IParsedInvoiceProvider parsedInvoiceProvider,
        IParsedInvoiceProcessingProvider processingProvider,
        IAzureBlobStorageProvider blobStorageProvider,
        IInvoiceParsingService invoiceParsingService,
        IIsdocGeneratorService isdocGenerator,
        IIsdocValidationService isdocValidator,
        ILogger<PdfInvoiceProcessingService> logger)
    {
        _parsedInvoiceProvider = parsedInvoiceProvider;
        _processingProvider = processingProvider;
        _blobStorageProvider = blobStorageProvider;
        _invoiceParsingService = invoiceParsingService;
        _isdocGenerator = isdocGenerator;
        _isdocValidator = isdocValidator;
        _logger = logger;
    }

    public async Task ProcessPdfInvoiceAsync(Guid parsedInvoiceId, Guid processingId)
    {
        var stepErrors = new Dictionary<string, string>();
        string? openAiFileId = null;

        try
        {
            _logger.LogInformation("Starting PDF invoice processing: {ParsedInvoiceId}, Processing: {ProcessingId}", parsedInvoiceId, processingId);

            // Update status to InProgress
            await _processingProvider.UpdateProcessingStatusAsync(processingId, ProcessingStatus.InProgress);

            // Get parsed invoice
            var parsedInvoice = await _parsedInvoiceProvider.GetByIdAsync(parsedInvoiceId);
            if (parsedInvoice == null)
            {
                throw new InvalidOperationException($"ParsedInvoice not found: {parsedInvoiceId}");
            }

            if (!parsedInvoice.LineMode.HasValue)
            {
                throw new InvalidOperationException("LineMode is required for PDF processing");
            }

            // Step 1: Upload to OpenAI
            await _processingProvider.UpdateCurrentStepAsync(processingId, "UploadingToOpenAI");
            await _parsedInvoiceProvider.UpdateStatusAsync(parsedInvoiceId, ParsedInvoiceStatus.Parsing);

            try
            {
                using var pdfStream = await _blobStorageProvider.DownloadBlobAsync(parsedInvoice.BlobContainerName, parsedInvoice.BlobName);
                openAiFileId = await _invoiceParsingService.UploadPdfAsync(pdfStream, parsedInvoice.FileName);
                _logger.LogInformation("PDF uploaded to OpenAI: {FileId}", openAiFileId);
            }
            catch (Exception ex)
            {
                stepErrors["UploadingToOpenAI"] = ex.Message;
                _logger.LogError(ex, "Failed to upload PDF to OpenAI");
                throw;
            }

            // Step 2: Extract data with OpenAI
            await _processingProvider.UpdateCurrentStepAsync(processingId, "ExtractingData");

            Models.Extraction.InvoiceExtractionResult extractionResult;
            try
            {
                extractionResult = await _invoiceParsingService.ExtractInvoiceDataAsync(openAiFileId, parsedInvoice.LineMode.Value);

                if (!extractionResult.Success || string.IsNullOrEmpty(extractionResult.InvoiceNumber))
                {
                    throw new InvalidOperationException($"OpenAI extraction failed: {extractionResult.ErrorMessage ?? "Unknown error"}");
                }

                _logger.LogInformation("Data extracted successfully for invoice: {InvoiceNumber}", extractionResult.InvoiceNumber);

                // Store OpenAI metadata
                var requestJson = JsonSerializer.Serialize(new { fileId = openAiFileId, lineMode = parsedInvoice.LineMode.Value });
                var responseJson = JsonSerializer.Serialize(extractionResult);

                await _parsedInvoiceProvider.UpdateOpenAiMetadataAsync(
                    parsedInvoiceId,
                    extractionResult.Model,
                    extractionResult.PromptTokens,
                    extractionResult.CompletionTokens,
                    requestJson,
                    responseJson);
            }
            catch (Exception ex)
            {
                stepErrors["ExtractingData"] = ex.Message;
                _logger.LogError(ex, "Failed to extract data from PDF");
                throw;
            }

            // Step 3: Generate ISDOC XML
            await _processingProvider.UpdateCurrentStepAsync(processingId, "GeneratingISDOC");

            string isdocXml;
            try
            {
                isdocXml = await _isdocGenerator.GenerateIsdocXmlAsync(extractionResult);
                _logger.LogInformation("ISDOC XML generated for invoice: {InvoiceNumber}", extractionResult.InvoiceNumber);

                // Upload generated ISDOC to Azure Blob Storage
                var isdocBlobName = $"{parsedInvoice.BlobName.Replace(".pdf", "")}_generated.xml";
                using var isdocStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(isdocXml));

                await _blobStorageProvider.UploadBlobAsync(
                    parsedInvoice.BlobContainerName,
                    isdocBlobName,
                    isdocStream,
                    "application/xml");

                var isdocBlobUrl = _blobStorageProvider.GetBlobUrl(parsedInvoice.BlobContainerName, isdocBlobName);
                await _parsedInvoiceProvider.UpdateGeneratedIsdocBlobAsync(parsedInvoiceId, isdocBlobName, isdocBlobUrl);

                _logger.LogInformation("Generated ISDOC XML uploaded to blob storage: {BlobName}", isdocBlobName);
            }
            catch (Exception ex)
            {
                stepErrors["GeneratingISDOC"] = ex.Message;
                _logger.LogError(ex, "Failed to generate ISDOC XML");
                throw;
            }

            // Step 4: Validate ISDOC against XSD schema
            await _processingProvider.UpdateCurrentStepAsync(processingId, "ValidatingXSD");

            try
            {
                var (isValid, errors) = await _isdocValidator.ValidateAgainstXsdAsync(isdocXml);

                if (!isValid)
                {
                    var errorMessage = string.Join("; ", errors);
                    stepErrors["ValidatingXSD"] = errorMessage;
                    _logger.LogWarning("ISDOC XML validation failed for invoice: {InvoiceNumber}. Errors: {Errors}",
                        extractionResult.InvoiceNumber, errorMessage);

                    // Update as validation failed
                    await UpdateParsedInvoiceData(parsedInvoiceId, extractionResult, false, errorMessage);
                    await _parsedInvoiceProvider.UpdateStatusAsync(parsedInvoiceId, ParsedInvoiceStatus.ValidationFailed);
                    await _processingProvider.FailProcessingAsync(processingId, $"XSD validation failed: {errorMessage}");
                    await UpdateStepErrors(processingId, stepErrors);

                    return;
                }

                _logger.LogInformation("ISDOC XML validation successful for invoice: {InvoiceNumber}", extractionResult.InvoiceNumber);
            }
            catch (Exception ex)
            {
                stepErrors["ValidatingXSD"] = ex.Message;
                _logger.LogError(ex, "Error during XSD validation");
                throw;
            }

            // Step 5: Update ParsedInvoice with extracted data
            await UpdateParsedInvoiceData(parsedInvoiceId, extractionResult, true, null);

            // Mark as parsed and ready to import
            await _parsedInvoiceProvider.UpdateStatusAsync(parsedInvoiceId, ParsedInvoiceStatus.Parsed);
            await _processingProvider.CompleteProcessingAsync(processingId);

            _logger.LogInformation("PDF invoice processing completed successfully: {ParsedInvoiceId}", parsedInvoiceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF invoice processing failed: {ParsedInvoiceId}", parsedInvoiceId);

            await _parsedInvoiceProvider.UpdateStatusAsync(parsedInvoiceId, ParsedInvoiceStatus.ValidationFailed);
            await _processingProvider.FailProcessingAsync(processingId, ex.Message);
            await UpdateStepErrors(processingId, stepErrors);

            throw;
        }
        finally
        {
            // Clean up: Delete file from OpenAI (best effort)
            if (!string.IsNullOrEmpty(openAiFileId))
            {
                try
                {
                    await _invoiceParsingService.DeleteFileAsync(openAiFileId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete OpenAI file: {FileId}", openAiFileId);
                }
            }
        }
    }

    public async Task RetryProcessingAsync(Guid parsedInvoiceId)
    {
        _logger.LogInformation("Retrying PDF invoice processing: {ParsedInvoiceId}", parsedInvoiceId);

        var parsedInvoice = await _parsedInvoiceProvider.GetByIdAsync(parsedInvoiceId);
        if (parsedInvoice == null)
        {
            throw new InvalidOperationException($"ParsedInvoice not found: {parsedInvoiceId}");
        }

        // Get existing processing attempts
        var processings = await _processingProvider.GetProcessingsByParsedInvoiceIdAsync(parsedInvoiceId);
        var attemptNumber = processings.Any() ? processings.Max(p => p.AttemptNumber) + 1 : 1;

        // Create new processing attempt
        var processing = await _processingProvider.CreateProcessingAsync(parsedInvoiceId, attemptNumber);

        // Reset status
        await _parsedInvoiceProvider.UpdateStatusAsync(parsedInvoiceId, ParsedInvoiceStatus.Uploaded);

        // Process
        await ProcessPdfInvoiceAsync(parsedInvoiceId, processing.Id);
    }

    private async Task UpdateParsedInvoiceData(
        Guid parsedInvoiceId,
        Models.Extraction.InvoiceExtractionResult extraction,
        bool isValid,
        string? validationErrors)
    {
        var parsedInvoice = await _parsedInvoiceProvider.GetByIdAsync(parsedInvoiceId);
        if (parsedInvoice == null)
        {
            throw new InvalidOperationException($"ParsedInvoice not found: {parsedInvoiceId}");
        }

        // Update all extracted fields
        parsedInvoice.InvoiceNumber = extraction.InvoiceNumber;
        parsedInvoice.DocumentType = extraction.DocumentType;
        parsedInvoice.IssuedOn = extraction.IssuedOn?.ToUniversalTime();
        parsedInvoice.DueOn = extraction.DueOn?.ToUniversalTime();
        parsedInvoice.TaxableSupplyDate = extraction.TaxableSupplyDate?.ToUniversalTime();

        parsedInvoice.Subtotal = extraction.Subtotal;
        parsedInvoice.Total = extraction.Total;
        parsedInvoice.Currency = extraction.Currency;
        parsedInvoice.VatPriceMode = extraction.VatPriceMode;

        // Supplier
        parsedInvoice.SupplierName = extraction.Supplier.Name;
        parsedInvoice.SupplierStreet = extraction.Supplier.Street;
        parsedInvoice.SupplierCity = extraction.Supplier.City;
        parsedInvoice.SupplierZip = extraction.Supplier.Zip;
        parsedInvoice.SupplierCountry = extraction.Supplier.Country;
        parsedInvoice.SupplierRegistrationNo = extraction.Supplier.RegistrationNo;
        parsedInvoice.SupplierVatNo = extraction.Supplier.VatNo;

        // Customer
        parsedInvoice.CustomerName = extraction.Customer.Name;
        parsedInvoice.CustomerStreet = extraction.Customer.Street;
        parsedInvoice.CustomerCity = extraction.Customer.City;
        parsedInvoice.CustomerZip = extraction.Customer.Zip;
        parsedInvoice.CustomerCountry = extraction.Customer.Country;
        parsedInvoice.CustomerRegistrationNo = extraction.Customer.RegistrationNo;
        parsedInvoice.CustomerVatNo = extraction.Customer.VatNo;

        // Payment
        parsedInvoice.VariableSymbol = extraction.VariableSymbol;
        parsedInvoice.ConstantSymbol = extraction.ConstantSymbol;
        parsedInvoice.SpecificSymbol = extraction.SpecificSymbol;
        parsedInvoice.BankAccount = extraction.BankAccount;
        parsedInvoice.Iban = extraction.Iban;
        parsedInvoice.Note = extraction.Note;

        // JSON data
        parsedInvoice.LinesJson = JsonSerializer.Serialize(extraction.Lines);
        parsedInvoice.VatRatesSummary = JsonSerializer.Serialize(extraction.VatRates);

        // Validation
        parsedInvoice.IsValid = isValid;
        parsedInvoice.ValidationErrors = validationErrors;
        parsedInvoice.ParsedAt = DateTime.UtcNow;

        await _parsedInvoiceProvider.UpdateParsedDataAsync(parsedInvoiceId, parsedInvoice);
    }

    private async Task UpdateStepErrors(Guid processingId, Dictionary<string, string> stepErrors)
    {
        if (stepErrors.Any())
        {
            await _processingProvider.UpdateStepErrorsAsync(processingId, stepErrors);
        }
    }
}
