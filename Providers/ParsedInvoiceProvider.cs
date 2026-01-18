using Isdocovac.Data;
using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers;

public interface IParsedInvoiceProvider
{
    Task<ParsedInvoice> CreateUploadAsync(Guid userId, string fileName, long fileSize, string contentType, Stream fileContent);
    Task<IEnumerable<ParsedInvoice>> GetUserParsedInvoicesAsync(Guid userId, ParsedInvoiceStatus? status = null);
    Task<ParsedInvoice?> GetByIdAsync(Guid parsedInvoiceId);
    Task<ParsedInvoice?> GetWithProcessingsAsync(Guid parsedInvoiceId);
    Task UpdateStatusAsync(Guid parsedInvoiceId, ParsedInvoiceStatus status);
    Task UpdateParsedDataAsync(Guid parsedInvoiceId, ParsedInvoice updatedData);
    Task MarkAsImportedAsync(Guid parsedInvoiceId, Guid importedInvoiceId);
    Task<Stream> GetFileContentAsync(Guid parsedInvoiceId);
    Task<string> GetSasUrlAsync(Guid parsedInvoiceId, int expirationMinutes = 60);
    Task DeleteAsync(Guid parsedInvoiceId);
}

public class ParsedInvoiceProvider : IParsedInvoiceProvider
{
    private readonly ApplicationDbContext _context;
    private readonly IAzureBlobStorageProvider _blobStorageProvider;
    private readonly IConfiguration _configuration;

    public ParsedInvoiceProvider(ApplicationDbContext context, IAzureBlobStorageProvider blobStorageProvider, IConfiguration configuration)
    {
        _context = context;
        _blobStorageProvider = blobStorageProvider;
        _configuration = configuration;
    }

    public async Task<ParsedInvoice> CreateUploadAsync(Guid userId, string fileName, long fileSize, string contentType, Stream fileContent)
    {
        var uploadId = Guid.NewGuid();
        var containerName = _configuration["AzureStorage:InvoiceContainerName"] ?? "invoice-uploads";
        var blobName = $"{userId}/{uploadId}/{fileName}";

        // Upload to Azure Blob Storage
        var blobUrl = await _blobStorageProvider.UploadBlobAsync(containerName, blobName, fileContent, contentType);

        var parsedInvoice = new ParsedInvoice
        {
            Id = uploadId,
            UserId = userId,
            FileName = fileName,
            FileSize = fileSize,
            ContentType = contentType,
            BlobContainerName = containerName,
            BlobName = blobName,
            BlobUrl = blobUrl,
            UploadedAt = DateTime.UtcNow,
            Status = ParsedInvoiceStatus.Uploaded,
            IsValid = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ParsedInvoices.Add(parsedInvoice);
        await _context.SaveChangesAsync();
        return parsedInvoice;
    }

    public async Task<IEnumerable<ParsedInvoice>> GetUserParsedInvoicesAsync(Guid userId, ParsedInvoiceStatus? status = null)
    {
        var query = _context.ParsedInvoices
            .Where(p => p.UserId == userId);

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        return await query
            .OrderByDescending(p => p.UploadedAt)
            .ToListAsync();
    }

    public async Task<ParsedInvoice?> GetByIdAsync(Guid parsedInvoiceId)
    {
        return await _context.ParsedInvoices
            .FirstOrDefaultAsync(p => p.Id == parsedInvoiceId);
    }

    public async Task<ParsedInvoice?> GetWithProcessingsAsync(Guid parsedInvoiceId)
    {
        return await _context.ParsedInvoices
            .Include(p => p.Processings)
            .FirstOrDefaultAsync(p => p.Id == parsedInvoiceId);
    }

    public async Task UpdateStatusAsync(Guid parsedInvoiceId, ParsedInvoiceStatus status)
    {
        var parsedInvoice = await _context.ParsedInvoices.FindAsync(parsedInvoiceId);
        if (parsedInvoice != null)
        {
            parsedInvoice.Status = status;
            parsedInvoice.UpdatedAt = DateTime.UtcNow;

            if (status == ParsedInvoiceStatus.Parsed)
            {
                parsedInvoice.ParsedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateParsedDataAsync(Guid parsedInvoiceId, ParsedInvoice updatedData)
    {
        var parsedInvoice = await _context.ParsedInvoices.FindAsync(parsedInvoiceId);
        if (parsedInvoice != null)
        {
            // Update parsed data fields
            parsedInvoice.InvoiceNumber = updatedData.InvoiceNumber;
            parsedInvoice.CustomId = updatedData.CustomId;
            parsedInvoice.IssuedOn = updatedData.IssuedOn;
            parsedInvoice.DueOn = updatedData.DueOn;
            parsedInvoice.DocumentType = updatedData.DocumentType;
            parsedInvoice.Subtotal = updatedData.Subtotal;
            parsedInvoice.Total = updatedData.Total;
            parsedInvoice.Currency = updatedData.Currency;
            parsedInvoice.VatPriceMode = updatedData.VatPriceMode;

            // Supplier fields
            parsedInvoice.SupplierName = updatedData.SupplierName;
            parsedInvoice.SupplierStreet = updatedData.SupplierStreet;
            parsedInvoice.SupplierCity = updatedData.SupplierCity;
            parsedInvoice.SupplierZip = updatedData.SupplierZip;
            parsedInvoice.SupplierCountry = updatedData.SupplierCountry;
            parsedInvoice.SupplierRegistrationNo = updatedData.SupplierRegistrationNo;
            parsedInvoice.SupplierVatNo = updatedData.SupplierVatNo;

            // Customer fields
            parsedInvoice.CustomerName = updatedData.CustomerName;
            parsedInvoice.CustomerStreet = updatedData.CustomerStreet;
            parsedInvoice.CustomerCity = updatedData.CustomerCity;
            parsedInvoice.CustomerZip = updatedData.CustomerZip;
            parsedInvoice.CustomerCountry = updatedData.CustomerCountry;
            parsedInvoice.CustomerRegistrationNo = updatedData.CustomerRegistrationNo;
            parsedInvoice.CustomerVatNo = updatedData.CustomerVatNo;

            // Payment info
            parsedInvoice.VariableSymbol = updatedData.VariableSymbol;
            parsedInvoice.ConstantSymbol = updatedData.ConstantSymbol;
            parsedInvoice.SpecificSymbol = updatedData.SpecificSymbol;
            parsedInvoice.BankAccount = updatedData.BankAccount;
            parsedInvoice.Iban = updatedData.Iban;
            parsedInvoice.Note = updatedData.Note;

            // JSON fields
            parsedInvoice.ParsedDataJson = updatedData.ParsedDataJson;
            parsedInvoice.LinesJson = updatedData.LinesJson;
            parsedInvoice.VatRatesSummary = updatedData.VatRatesSummary;

            parsedInvoice.IsValid = updatedData.IsValid;
            parsedInvoice.ValidationErrors = updatedData.ValidationErrors;
            parsedInvoice.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAsImportedAsync(Guid parsedInvoiceId, Guid importedInvoiceId)
    {
        var parsedInvoice = await _context.ParsedInvoices.FindAsync(parsedInvoiceId);
        if (parsedInvoice != null)
        {
            parsedInvoice.Status = ParsedInvoiceStatus.Imported;
            parsedInvoice.ImportedToInvoiceAt = DateTime.UtcNow;
            parsedInvoice.ImportedInvoiceId = importedInvoiceId;
            parsedInvoice.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }
    }

    public async Task<Stream> GetFileContentAsync(Guid parsedInvoiceId)
    {
        var parsedInvoice = await _context.ParsedInvoices.FindAsync(parsedInvoiceId);
        if (parsedInvoice == null)
        {
            throw new InvalidOperationException($"ParsedInvoice with ID {parsedInvoiceId} not found");
        }

        return await _blobStorageProvider.DownloadBlobAsync(parsedInvoice.BlobContainerName, parsedInvoice.BlobName);
    }

    public async Task<string> GetSasUrlAsync(Guid parsedInvoiceId, int expirationMinutes = 60)
    {
        var parsedInvoice = await _context.ParsedInvoices.FindAsync(parsedInvoiceId);
        if (parsedInvoice == null)
        {
            throw new InvalidOperationException($"ParsedInvoice with ID {parsedInvoiceId} not found");
        }

        return await _blobStorageProvider.GenerateSasUrlAsync(parsedInvoice.BlobContainerName, parsedInvoice.BlobName, expirationMinutes);
    }

    public async Task DeleteAsync(Guid parsedInvoiceId)
    {
        var parsedInvoice = await _context.ParsedInvoices.FindAsync(parsedInvoiceId);
        if (parsedInvoice != null)
        {
            // Delete from Azure Blob Storage
            await _blobStorageProvider.DeleteBlobAsync(parsedInvoice.BlobContainerName, parsedInvoice.BlobName);

            // Delete from database
            _context.ParsedInvoices.Remove(parsedInvoice);
            await _context.SaveChangesAsync();
        }
    }
}
