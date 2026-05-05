using Isdocovac.Data;
using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers.Investments;

public interface IBrokerImportProvider
{
    Task<BrokerImport> CreateAsync(BrokerImport entity);
    Task<BrokerImport?> GetAsync(Guid id);
    Task<IReadOnlyList<BrokerImport>> ListByCompanyAsync(Guid companyId);
    Task<BrokerImport?> FindByHashAsync(Guid companyId, string fileHash);
    Task UpdateAsync(BrokerImport entity);
    Task DeleteAsync(Guid id);
}

public class BrokerImportProvider : IBrokerImportProvider
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public BrokerImportProvider(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<BrokerImport> CreateAsync(BrokerImport entity)
    {
        var now = DateTime.UtcNow;
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        if (entity.UploadedAt == default)
        {
            entity.UploadedAt = now;
        }

        await using var context = _contextFactory.CreateDbContext();
        context.BrokerImports.Add(entity);
        await context.SaveChangesAsync();
        return entity;
    }

    public async Task<BrokerImport?> GetAsync(Guid id)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.BrokerImports.FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<IReadOnlyList<BrokerImport>> ListByCompanyAsync(Guid companyId)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.BrokerImports
            .Where(b => b.CompanyId == companyId)
            .OrderByDescending(b => b.UploadedAt)
            .ToListAsync();
    }

    public async Task<BrokerImport?> FindByHashAsync(Guid companyId, string fileHash)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.BrokerImports
            .FirstOrDefaultAsync(b => b.CompanyId == companyId && b.FileHash == fileHash);
    }

    public async Task UpdateAsync(BrokerImport entity)
    {
        await using var context = _contextFactory.CreateDbContext();
        var existing = await context.BrokerImports.FirstOrDefaultAsync(b => b.Id == entity.Id)
            ?? throw new InvalidOperationException($"BrokerImport {entity.Id} not found.");

        existing.Status = entity.Status;
        existing.RowsTotal = entity.RowsTotal;
        existing.RowsImported = entity.RowsImported;
        existing.RowsSkipped = entity.RowsSkipped;
        existing.RowsDuplicate = entity.RowsDuplicate;
        existing.ParsedAt = entity.ParsedAt;
        existing.ImportedAt = entity.ImportedAt;
        existing.ErrorMessage = entity.ErrorMessage;
        existing.Notes = entity.Notes;
        existing.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var context = _contextFactory.CreateDbContext();
        var existing = await context.BrokerImports.FirstOrDefaultAsync(b => b.Id == id);
        if (existing != null)
        {
            context.BrokerImports.Remove(existing);
            await context.SaveChangesAsync();
        }
    }
}
