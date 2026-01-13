using Isdocovac.Data;
using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers;

public interface IInvoiceProcessingProvider
{
    Task<InvoiceProcessing> CreateProcessingAsync(
        Guid invoiceUploadId,
        int attemptNumber);

    Task<InvoiceProcessing?> GetProcessingByIdAsync(Guid processingId);
    Task<IEnumerable<InvoiceProcessing>> GetProcessingsByInvoiceIdAsync(Guid invoiceUploadId);
    Task<InvoiceProcessing?> GetLatestProcessingForInvoiceAsync(Guid invoiceUploadId);

    Task UpdateProcessingStatusAsync(
        Guid processingId,
        ProcessingStatus status,
        string? errorMessage = null);

    Task CompleteProcessingAsync(
        Guid processingId,
        Guid parsedIsdocId);

    Task FailProcessingAsync(
        Guid processingId,
        string errorMessage);
}

public class InvoiceProcessingProvider : IInvoiceProcessingProvider
{
    private readonly ApplicationDbContext _context;

    public InvoiceProcessingProvider(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<InvoiceProcessing> CreateProcessingAsync(
        Guid invoiceUploadId,
        int attemptNumber)
    {
        var processing = new InvoiceProcessing
        {
            Id = Guid.NewGuid(),
            InvoiceUploadId = invoiceUploadId,
            StartedAt = DateTime.UtcNow,
            Status = (int)ProcessingStatus.Pending,
            AttemptNumber = attemptNumber
        };

        _context.InvoiceProcessings.Add(processing);
        await _context.SaveChangesAsync();
        return processing;
    }

    public async Task<InvoiceProcessing?> GetProcessingByIdAsync(Guid processingId)
    {
        return await _context.InvoiceProcessings
            .Include(p => p.ParsedIsdoc)
            .FirstOrDefaultAsync(p => p.Id == processingId);
    }

    public async Task<IEnumerable<InvoiceProcessing>> GetProcessingsByInvoiceIdAsync(Guid invoiceUploadId)
    {
        return await _context.InvoiceProcessings
            .Where(p => p.InvoiceUploadId == invoiceUploadId)
            .Include(p => p.ParsedIsdoc)
            .OrderByDescending(p => p.StartedAt)
            .ToListAsync();
    }

    public async Task<InvoiceProcessing?> GetLatestProcessingForInvoiceAsync(Guid invoiceUploadId)
    {
        return await _context.InvoiceProcessings
            .Where(p => p.InvoiceUploadId == invoiceUploadId)
            .Include(p => p.ParsedIsdoc)
            .OrderByDescending(p => p.StartedAt)
            .FirstOrDefaultAsync();
    }

    public async Task UpdateProcessingStatusAsync(
        Guid processingId,
        ProcessingStatus status,
        string? errorMessage = null)
    {
        var processing = await _context.InvoiceProcessings.FindAsync(processingId);
        if (processing != null)
        {
            processing.Status = (int)status;
            if (errorMessage != null)
            {
                processing.ErrorMessage = errorMessage;
            }

            if (status == ProcessingStatus.Completed || status == ProcessingStatus.Failed)
            {
                processing.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }
    }

    public async Task CompleteProcessingAsync(
        Guid processingId,
        Guid parsedIsdocId)
    {
        var processing = await _context.InvoiceProcessings.FindAsync(processingId);
        if (processing != null)
        {
            processing.Status = (int)ProcessingStatus.Completed;
            processing.ParsedIsdocId = parsedIsdocId;
            processing.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task FailProcessingAsync(
        Guid processingId,
        string errorMessage)
    {
        var processing = await _context.InvoiceProcessings.FindAsync(processingId);
        if (processing != null)
        {
            processing.Status = (int)ProcessingStatus.Failed;
            processing.ErrorMessage = errorMessage;
            processing.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}