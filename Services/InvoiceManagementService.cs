using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Isdocovac.Providers;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Services;

public interface IInvoiceManagementService
{
    Task<IEnumerable<Invoice>> GetCompanyInvoicesAsync(Guid companyId, InvoiceDirection? direction = null, int page = 1, int pageSize = 50);
    Task<int> GetCompanyInvoiceCountAsync(Guid companyId, InvoiceDirection? direction = null);
    Task<Invoice?> GetInvoiceDetailsAsync(Guid invoiceId);
    Task<string> GenerateInternalNumberAsync(Guid companyId, DateTime vatDate);
    Task<Invoice> CreateManualInvoiceAsync(Invoice invoice);
    Task<Invoice> CreateManualInvoiceWithAttachmentAsync(Invoice invoice, IBrowserFile? pdfFile = null);
    Task UpdateInvoiceAsync(Invoice invoice);
    Task UpdateInvoiceWithAttachmentAsync(Invoice invoice, IBrowserFile? newPdfFile = null, Guid? existingAttachmentIdToReplace = null);
    Task DeleteInvoiceAsync(Guid invoiceId);
}

public class InvoiceManagementService : IInvoiceManagementService
{
    private readonly IMainInvoiceProvider _invoiceProvider;
    private readonly IInvoiceAttachmentProvider _attachmentProvider;
    private readonly IAzureBlobStorageProvider _blobStorageProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InvoiceManagementService> _logger;

    public InvoiceManagementService(
        IMainInvoiceProvider invoiceProvider,
        IInvoiceAttachmentProvider attachmentProvider,
        IAzureBlobStorageProvider blobStorageProvider,
        IConfiguration configuration,
        ILogger<InvoiceManagementService> logger)
    {
        _invoiceProvider = invoiceProvider;
        _attachmentProvider = attachmentProvider;
        _blobStorageProvider = blobStorageProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IEnumerable<Invoice>> GetCompanyInvoicesAsync(Guid companyId, InvoiceDirection? direction = null, int page = 1, int pageSize = 50)
    {
        return await _invoiceProvider.GetByCompanyIdAsync(companyId, direction, page, pageSize);
    }

    public async Task<int> GetCompanyInvoiceCountAsync(Guid companyId, InvoiceDirection? direction = null)
    {
        return await _invoiceProvider.GetCountAsync(companyId, direction);
    }

    public async Task<Invoice?> GetInvoiceDetailsAsync(Guid invoiceId)
    {
        return await _invoiceProvider.GetWithDetailsAsync(invoiceId);
    }

    public async Task<string> GenerateInternalNumberAsync(Guid companyId, DateTime vatDate)
    {
        var suffix = await _invoiceProvider.GetNextInternalNumberSuffixAsync(companyId, vatDate.Year, vatDate.Month);
        return $"{vatDate.Year:0000}{vatDate.Month:00}{suffix:0000}";
    }

    public async Task<Invoice> CreateManualInvoiceAsync(Invoice invoice)
    {
        invoice.Source = InvoiceSource.Manual;
        return await CreateWithInternalNumberRetryAsync(invoice);
    }

    /// <summary>
    /// Inserts an invoice and, on a unique-constraint clash on InternalNumber, regenerates the
    /// suffix and retries. Protects against a race when two requests pick the same next suffix.
    /// </summary>
    internal async Task<Invoice> CreateWithInternalNumberRetryAsync(Invoice invoice)
    {
        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await _invoiceProvider.CreateAsync(invoice);
            }
            catch (DbUpdateException ex) when (IsInternalNumberUniqueViolation(ex) && attempt < maxAttempts)
            {
                _logger.LogWarning(ex, "InternalNumber collision for {InternalNumber}, regenerating (attempt {Attempt}/{MaxAttempts})",
                    invoice.InternalNumber, attempt, maxAttempts);

                var vatDate = invoice.TaxableSupplyDate ?? invoice.IssuedOn ?? DateTime.UtcNow;
                invoice.InternalNumber = await GenerateInternalNumberAsync(invoice.CompanyId, vatDate);
                invoice.Id = Guid.Empty; // force new id on next attempt
            }
        }

        throw new InvalidOperationException("Failed to assign a unique InternalNumber after multiple attempts.");
    }

    private static bool IsInternalNumberUniqueViolation(DbUpdateException ex)
    {
        // Postgres unique violation SQLSTATE is 23505. Match by message contains internal_number to avoid
        // mistaking unrelated unique constraint failures.
        return ex.InnerException?.Message.Contains("internal_number", StringComparison.OrdinalIgnoreCase) == true;
    }

    public async Task UpdateInvoiceAsync(Invoice invoice)
    {
        await _invoiceProvider.UpdateAsync(invoice);
    }

    public async Task DeleteInvoiceAsync(Guid invoiceId)
    {
        await _invoiceProvider.DeleteAsync(invoiceId);
    }

    public async Task<Invoice> CreateManualInvoiceWithAttachmentAsync(Invoice invoice, IBrowserFile? pdfFile = null)
    {
        var createdInvoice = await CreateManualInvoiceAsync(invoice);

        if (pdfFile != null)
        {
            await UploadPdfAttachmentAsync(createdInvoice.Id, createdInvoice.CompanyId, pdfFile);
        }

        return createdInvoice;
    }

    public async Task UpdateInvoiceWithAttachmentAsync(Invoice invoice, IBrowserFile? newPdfFile = null, Guid? existingAttachmentIdToReplace = null)
    {
        await UpdateInvoiceAsync(invoice);

        if (newPdfFile != null)
        {
            if (existingAttachmentIdToReplace.HasValue)
            {
                var oldAttachment = await _attachmentProvider.GetByIdAsync(existingAttachmentIdToReplace.Value);
                if (oldAttachment != null && !string.IsNullOrEmpty(oldAttachment.BlobContainerName) && !string.IsNullOrEmpty(oldAttachment.BlobName))
                {
                    await _blobStorageProvider.DeleteBlobAsync(oldAttachment.BlobContainerName, oldAttachment.BlobName);
                    await _attachmentProvider.DeleteAsync(existingAttachmentIdToReplace.Value);
                }
            }

            await UploadPdfAttachmentAsync(invoice.Id, invoice.CompanyId, newPdfFile);
        }
    }

    private async Task UploadPdfAttachmentAsync(Guid invoiceId, Guid companyId, IBrowserFile pdfFile)
    {
        const long maxFileSize = 10 * 1024 * 1024; // 10 MB

        if (pdfFile.Size > maxFileSize)
        {
            throw new InvalidOperationException($"File size exceeds maximum allowed size of 10 MB");
        }

        if (!pdfFile.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only PDF files are allowed");
        }

        var containerName = _configuration["AzureStorage:InvoiceContainerName"] ?? "invoice-uploads";
        var blobName = $"{companyId}/invoices/{invoiceId}/{pdfFile.Name}";

        using var stream = pdfFile.OpenReadStream(maxFileSize);
        var blobUrl = await _blobStorageProvider.UploadBlobAsync(
            containerName, blobName, stream, pdfFile.ContentType);

        var attachment = new InvoiceAttachment
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            Filename = pdfFile.Name,
            ContentType = pdfFile.ContentType,
            BlobContainerName = containerName,
            BlobName = blobName,
            BlobUrl = blobUrl,
            CreatedAt = DateTime.UtcNow
        };

        await _attachmentProvider.CreateAsync(attachment);

        _logger.LogInformation("Uploaded PDF attachment {Filename} for invoice {InvoiceId}", pdfFile.Name, invoiceId);
    }
}
