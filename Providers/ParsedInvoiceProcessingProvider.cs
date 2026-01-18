using Isdocovac.Data;
using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers;

public interface IParsedInvoiceProcessingProvider
{
    Task<ParsedInvoiceProcessing> CreateProcessingAsync(
        Guid parsedInvoiceId,
        int attemptNumber);

    Task<ParsedInvoiceProcessing?> GetProcessingByIdAsync(Guid processingId);
    Task<IEnumerable<ParsedInvoiceProcessing>> GetProcessingsByParsedInvoiceIdAsync(Guid parsedInvoiceId);
    Task<ParsedInvoiceProcessing?> GetLatestProcessingForParsedInvoiceAsync(Guid parsedInvoiceId);

    Task UpdateProcessingStatusAsync(
        Guid processingId,
        ProcessingStatus status,
        string? errorMessage = null);

    Task CompleteProcessingAsync(Guid processingId);

    Task FailProcessingAsync(
        Guid processingId,
        string errorMessage);
}

public class ParsedInvoiceProcessingProvider : IParsedInvoiceProcessingProvider
{
    private readonly ApplicationDbContext _context;

    public ParsedInvoiceProcessingProvider(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ParsedInvoiceProcessing> CreateProcessingAsync(
        Guid parsedInvoiceId,
        int attemptNumber)
    {
        var processing = new ParsedInvoiceProcessing
        {
            Id = Guid.NewGuid(),
            ParsedInvoiceId = parsedInvoiceId,
            StartedAt = DateTime.UtcNow,
            Status = ProcessingStatus.Pending,
            AttemptNumber = attemptNumber
        };

        _context.ParsedInvoiceProcessings.Add(processing);
        await _context.SaveChangesAsync();
        return processing;
    }

    public async Task<ParsedInvoiceProcessing?> GetProcessingByIdAsync(Guid processingId)
    {
        return await _context.ParsedInvoiceProcessings
            .Include(p => p.ParsedInvoice)
            .FirstOrDefaultAsync(p => p.Id == processingId);
    }

    public async Task<IEnumerable<ParsedInvoiceProcessing>> GetProcessingsByParsedInvoiceIdAsync(Guid parsedInvoiceId)
    {
        return await _context.ParsedInvoiceProcessings
            .Where(p => p.ParsedInvoiceId == parsedInvoiceId)
            .OrderByDescending(p => p.StartedAt)
            .ToListAsync();
    }

    public async Task<ParsedInvoiceProcessing?> GetLatestProcessingForParsedInvoiceAsync(Guid parsedInvoiceId)
    {
        return await _context.ParsedInvoiceProcessings
            .Where(p => p.ParsedInvoiceId == parsedInvoiceId)
            .OrderByDescending(p => p.StartedAt)
            .FirstOrDefaultAsync();
    }

    public async Task UpdateProcessingStatusAsync(
        Guid processingId,
        ProcessingStatus status,
        string? errorMessage = null)
    {
        var processing = await _context.ParsedInvoiceProcessings.FindAsync(processingId);
        if (processing != null)
        {
            processing.Status = status;
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

    public async Task CompleteProcessingAsync(Guid processingId)
    {
        var processing = await _context.ParsedInvoiceProcessings.FindAsync(processingId);
        if (processing != null)
        {
            processing.Status = ProcessingStatus.Completed;
            processing.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task FailProcessingAsync(
        Guid processingId,
        string errorMessage)
    {
        var processing = await _context.ParsedInvoiceProcessings.FindAsync(processingId);
        if (processing != null)
        {
            processing.Status = ProcessingStatus.Failed;
            processing.ErrorMessage = errorMessage;
            processing.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
