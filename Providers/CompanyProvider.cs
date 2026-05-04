using Isdocovac.Data;
using Isdocovac.Models;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers;

public interface ICompanyProvider
{
    Task<Company> CreateAsync(Company company);
    Task<Company?> GetByIdAsync(Guid companyId);
    Task<IReadOnlyList<Company>> ListByOwnerAsync(Guid ownerUserId);
    Task<Company> UpdateAsync(Company company);
    Task SoftDeleteAsync(Guid companyId);
    Task<bool> UserOwnsAsync(Guid userId, Guid companyId);
}

public class CompanyProvider : ICompanyProvider
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public CompanyProvider(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Company> CreateAsync(Company company)
    {
        var now = DateTime.UtcNow;
        company.Id = company.Id == Guid.Empty ? Guid.NewGuid() : company.Id;
        company.CreatedAt = now;
        company.UpdatedAt = now;
        company.IsActive = true;

        await using var context = _contextFactory.CreateDbContext();
        context.Companies.Add(company);
        await context.SaveChangesAsync();
        return company;
    }

    public async Task<Company?> GetByIdAsync(Guid companyId)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.Companies.FirstOrDefaultAsync(c => c.Id == companyId);
    }

    public async Task<IReadOnlyList<Company>> ListByOwnerAsync(Guid ownerUserId)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.Companies
            .Where(c => c.OwnerUserId == ownerUserId && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Company> UpdateAsync(Company company)
    {
        await using var context = _contextFactory.CreateDbContext();
        var existing = await context.Companies.FirstOrDefaultAsync(c => c.Id == company.Id)
            ?? throw new InvalidOperationException($"Company {company.Id} not found.");

        existing.Name = company.Name;
        existing.IsLegalEntity = company.IsLegalEntity;
        existing.CompanyName = company.CompanyName;
        existing.FirstName = company.FirstName;
        existing.LastName = company.LastName;
        existing.Titul = company.Titul;
        existing.Dic = company.Dic;
        existing.Ico = company.Ico;
        existing.Street = company.Street;
        existing.HouseNumber = company.HouseNumber;
        existing.OrientNumber = company.OrientNumber;
        existing.City = company.City;
        existing.Zip = company.Zip;
        existing.CountryCode = company.CountryCode;
        existing.Email = company.Email;
        existing.Phone = company.Phone;
        existing.TaxOfficeCode = company.TaxOfficeCode;
        existing.TaxOfficeBranchCode = company.TaxOfficeBranchCode;
        existing.OkecCode = company.OkecCode;
        existing.DataBoxId = company.DataBoxId;
        existing.VatPeriod = company.VatPeriod;
        existing.PreferredCurrency = string.IsNullOrWhiteSpace(company.PreferredCurrency)
            ? "CZK"
            : company.PreferredCurrency.ToUpperInvariant();
        existing.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return existing;
    }

    public async Task SoftDeleteAsync(Guid companyId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var c = await context.Companies.FindAsync(companyId);
        if (c != null)
        {
            c.IsActive = false;
            c.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }

    public async Task<bool> UserOwnsAsync(Guid userId, Guid companyId)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.Companies
            .AnyAsync(c => c.Id == companyId && c.OwnerUserId == userId && c.IsActive);
    }
}
