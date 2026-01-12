using Isdocovac.Data;
using Isdocovac.Models;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers;

public class ParsedIsdocProvider : IParsedIsdocProvider
{
    private readonly ApplicationDbContext _context;

    public ParsedIsdocProvider(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ParsedIsdoc> CreateParsedIsdocAsync(Guid invoiceUploadId, string parsedData, bool isValid, string? validationErrors = null)
    {
        var parsedIsdoc = new ParsedIsdoc
        {
            Id = Guid.NewGuid(),
            InvoiceUploadId = invoiceUploadId,
            ParsedAt = DateTime.UtcNow,
            ParsedData = parsedData,
            IsValid = isValid,
            ValidationErrors = validationErrors
        };

        _context.ParsedIsdocs.Add(parsedIsdoc);
        await _context.SaveChangesAsync();
        return parsedIsdoc;
    }

    public async Task<IEnumerable<ParsedIsdoc>> GetParsedIsdocsForUploadAsync(Guid invoiceUploadId)
    {
        return await _context.ParsedIsdocs
            .Where(p => p.InvoiceUploadId == invoiceUploadId)
            .OrderByDescending(p => p.ParsedAt)
            .ToListAsync();
    }

    public async Task<ParsedIsdoc?> GetLatestParsedIsdocForUploadAsync(Guid invoiceUploadId)
    {
        return await _context.ParsedIsdocs
            .Where(p => p.InvoiceUploadId == invoiceUploadId)
            .OrderByDescending(p => p.ParsedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<ParsedIsdoc?> GetParsedIsdocByIdAsync(Guid parsedIsdocId)
    {
        return await _context.ParsedIsdocs
            .FirstOrDefaultAsync(p => p.Id == parsedIsdocId);
    }

    public async Task UpdateParsedIsdocAsync(ParsedIsdoc parsedIsdoc)
    {
        _context.ParsedIsdocs.Update(parsedIsdoc);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteParsedIsdocAsync(Guid parsedIsdocId)
    {
        var parsedIsdoc = await _context.ParsedIsdocs.FindAsync(parsedIsdocId);
        if (parsedIsdoc != null)
        {
            _context.ParsedIsdocs.Remove(parsedIsdoc);
            await _context.SaveChangesAsync();
        }
    }
}
