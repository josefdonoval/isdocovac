using Isdocovac.Data;
using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers;

public interface IInvoiceProvider
{
    Task<InvoiceUpload> CreateUploadAsync(Guid userId, string fileName, long fileSize, string contentType, Stream fileContent);
    Task<IEnumerable<InvoiceUpload>> GetUserUploadsAsync(Guid userId);
    Task<InvoiceUpload?> GetUploadByIdAsync(Guid uploadId);
    Task<InvoiceUpload?> GetUploadWithParsedDataAsync(Guid uploadId);
    Task<Stream> GetUploadFileContentAsync(Guid uploadId);
    Task<string> GetUploadSasUrlAsync(Guid uploadId, int expirationMinutes = 60);
    Task UpdateUploadStatusAsync(Guid uploadId, InvoiceUploadStatus status);
    Task DeleteUploadAsync(Guid uploadId);
}

public class InvoiceProvider : IInvoiceProvider
{
    private readonly ApplicationDbContext _context;
    private readonly IAzureBlobStorageProvider _blobStorageProvider;
    private readonly IConfiguration _configuration;

    public InvoiceProvider(ApplicationDbContext context, IAzureBlobStorageProvider blobStorageProvider, IConfiguration configuration)
    {
        _context = context;
        _blobStorageProvider = blobStorageProvider;
        _configuration = configuration;
    }

    public async Task<InvoiceUpload> CreateUploadAsync(Guid userId, string fileName, long fileSize, string contentType, Stream fileContent)
    {
        var uploadId = Guid.NewGuid();
        var containerName = _configuration["AzureStorage:InvoiceContainerName"] ?? "invoice-uploads";
        var blobName = $"{userId}/{uploadId}/{fileName}";

        // Upload to Azure Blob Storage
        var blobUrl = await _blobStorageProvider.UploadBlobAsync(containerName, blobName, fileContent, contentType);

        var upload = new InvoiceUpload
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
            Status = InvoiceUploadStatus.Pending
        };

        _context.InvoiceUploads.Add(upload);
        await _context.SaveChangesAsync();
        return upload;
    }

    public async Task<IEnumerable<InvoiceUpload>> GetUserUploadsAsync(Guid userId)
    {
        return await _context.InvoiceUploads
            .Where(u => u.UserId == userId)
            .OrderByDescending(u => u.UploadedAt)
            .ToListAsync();
    }

    public async Task<InvoiceUpload?> GetUploadByIdAsync(Guid uploadId)
    {
        return await _context.InvoiceUploads
            .FirstOrDefaultAsync(u => u.Id == uploadId);
    }

    public async Task<InvoiceUpload?> GetUploadWithParsedDataAsync(Guid uploadId)
    {
        return await _context.InvoiceUploads
            .Include(u => u.ParsedIsdocs)
            .FirstOrDefaultAsync(u => u.Id == uploadId);
    }

    public async Task UpdateUploadStatusAsync(Guid uploadId, InvoiceUploadStatus status)
    {
        var upload = await _context.InvoiceUploads.FindAsync(uploadId);
        if (upload != null)
        {
            upload.Status = status;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Stream> GetUploadFileContentAsync(Guid uploadId)
    {
        var upload = await _context.InvoiceUploads.FindAsync(uploadId);
        if (upload == null)
        {
            throw new InvalidOperationException($"Upload with ID {uploadId} not found");
        }

        return await _blobStorageProvider.DownloadBlobAsync(upload.BlobContainerName, upload.BlobName);
    }

    public async Task<string> GetUploadSasUrlAsync(Guid uploadId, int expirationMinutes = 60)
    {
        var upload = await _context.InvoiceUploads.FindAsync(uploadId);
        if (upload == null)
        {
            throw new InvalidOperationException($"Upload with ID {uploadId} not found");
        }

        return await _blobStorageProvider.GenerateSasUrlAsync(upload.BlobContainerName, upload.BlobName, expirationMinutes);
    }

    public async Task DeleteUploadAsync(Guid uploadId)
    {
        var upload = await _context.InvoiceUploads.FindAsync(uploadId);
        if (upload != null)
        {
            // Delete from Azure Blob Storage
            await _blobStorageProvider.DeleteBlobAsync(upload.BlobContainerName, upload.BlobName);

            // Delete from database
            _context.InvoiceUploads.Remove(upload);
            await _context.SaveChangesAsync();
        }
    }
}
