using Isdocovac.Data;
using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers;

public class InvoiceProvider : IInvoiceProvider
{
    private readonly ApplicationDbContext _context;

    public InvoiceProvider(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<InvoiceUpload> CreateUploadAsync(Guid userId, string fileName, long fileSize, string contentType, string xmlContent)
    {
        var upload = new InvoiceUpload
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FileName = fileName,
            FileSize = fileSize,
            ContentType = contentType,
            RawXmlContent = xmlContent,
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

    public async Task DeleteUploadAsync(Guid uploadId)
    {
        var upload = await _context.InvoiceUploads.FindAsync(uploadId);
        if (upload != null)
        {
            _context.InvoiceUploads.Remove(upload);
            await _context.SaveChangesAsync();
        }
    }
}
