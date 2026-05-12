using Isdocovac.Data;
using Isdocovac.Models.Email;
using Isdocovac.Models.Enums;
using Isdocovac.Models.Inbox;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers.Inbox;

public record DeskRow(
    ExternalOriginFile File,
    EmailIngestionMessage? EmailMessage);

public interface IExternalOriginFileProvider
{
    Task<ExternalOriginFile> CreateAsync(ExternalOriginFile file);
    Task<ExternalOriginFile?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<DeskRow>> GetDeskAsync(Guid companyId, ExternalOriginFileStatus? status = ExternalOriginFileStatus.OnDesk);
    Task MarkImportedAsync(Guid id, Guid parsedInvoiceId);
    Task MarkDiscardedAsync(Guid id);

    /// <summary>
    /// Hard-deletes the listed files (DB rows + their blobs). Caller must have user confirmation
    /// — this is irreversible and may also remove blobs already linked to imported invoices.
    /// </summary>
    Task<int> DeleteManyAsync(Guid companyId, IEnumerable<Guid> ids);
}

public class ExternalOriginFileProvider : IExternalOriginFileProvider
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly IAzureBlobStorageProvider _blobStorageProvider;
    private readonly ILogger<ExternalOriginFileProvider> _logger;

    public ExternalOriginFileProvider(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        IAzureBlobStorageProvider blobStorageProvider,
        ILogger<ExternalOriginFileProvider> logger)
    {
        _contextFactory = contextFactory;
        _blobStorageProvider = blobStorageProvider;
        _logger = logger;
    }

    public async Task<ExternalOriginFile> CreateAsync(ExternalOriginFile file)
    {
        var now = DateTime.UtcNow;
        file.Id = file.Id == Guid.Empty ? Guid.NewGuid() : file.Id;
        file.CreatedAt = now;
        file.UpdatedAt = now;

        await using var context = _contextFactory.CreateDbContext();
        context.ExternalOriginFiles.Add(file);
        await context.SaveChangesAsync();
        return file;
    }

    public async Task<ExternalOriginFile?> GetByIdAsync(Guid id)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.ExternalOriginFiles.FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<IReadOnlyList<DeskRow>> GetDeskAsync(Guid companyId, ExternalOriginFileStatus? status = ExternalOriginFileStatus.OnDesk)
    {
        await using var context = _contextFactory.CreateDbContext();
        var query = context.ExternalOriginFiles
            .Where(f => f.CompanyId == companyId);
        if (status.HasValue)
        {
            query = query.Where(f => f.Status == status.Value);
        }

        // Outer-join via GroupJoin so non-Email origins still surface (no email metadata).
        var rows = await (
            from f in query
            join m in context.EmailIngestionMessages
                on f.EmailIngestionMessageId equals m.Id into joined
            from m in joined.DefaultIfEmpty()
            orderby f.CreatedAt descending
            select new DeskRow(f, m)
        ).ToListAsync();

        return rows;
    }

    public async Task MarkImportedAsync(Guid id, Guid parsedInvoiceId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var existing = await context.ExternalOriginFiles.FindAsync(id);
        if (existing == null) return;
        existing.Status = ExternalOriginFileStatus.Imported;
        existing.ParsedInvoiceId = parsedInvoiceId;
        existing.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public async Task MarkDiscardedAsync(Guid id)
    {
        await using var context = _contextFactory.CreateDbContext();
        var existing = await context.ExternalOriginFiles.FindAsync(id);
        if (existing == null) return;
        existing.Status = ExternalOriginFileStatus.Discarded;
        existing.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public async Task<int> DeleteManyAsync(Guid companyId, IEnumerable<Guid> ids)
    {
        var idSet = ids.Distinct().ToList();
        if (idSet.Count == 0) return 0;

        await using var context = _contextFactory.CreateDbContext();
        var toDelete = await context.ExternalOriginFiles
            .Where(f => f.CompanyId == companyId && idSet.Contains(f.Id))
            .ToListAsync();

        // Best-effort blob cleanup before DB removal. Each failure is logged but doesn't block the
        // others — the DB is the source of truth for "is this on the desk", a stray blob is harmless.
        foreach (var file in toDelete)
        {
            if (string.IsNullOrEmpty(file.BlobContainerName) || string.IsNullOrEmpty(file.BlobName))
                continue;
            try
            {
                await _blobStorageProvider.DeleteBlobAsync(file.BlobContainerName, file.BlobName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to delete blob {BlobName} for ExternalOriginFile {FileId}; row will still be removed.",
                    file.BlobName, file.Id);
            }
        }

        context.ExternalOriginFiles.RemoveRange(toDelete);
        await context.SaveChangesAsync();
        return toDelete.Count;
    }
}
