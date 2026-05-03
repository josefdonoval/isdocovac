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
    private readonly ApplicationDbContext _context;

    public CompanyProvider(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Company> CreateAsync(Company company)
    {
        var now = DateTime.UtcNow;
        company.Id = company.Id == Guid.Empty ? Guid.NewGuid() : company.Id;
        company.CreatedAt = now;
        company.UpdatedAt = now;
        company.IsActive = true;
        _context.Companies.Add(company);
        await _context.SaveChangesAsync();
        return company;
    }

    public Task<Company?> GetByIdAsync(Guid companyId)
        => _context.Companies.FirstOrDefaultAsync(c => c.Id == companyId);

    public async Task<IReadOnlyList<Company>> ListByOwnerAsync(Guid ownerUserId)
        => await _context.Companies
            .Where(c => c.OwnerUserId == ownerUserId && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();

    public async Task<Company> UpdateAsync(Company company)
    {
        var existing = await _context.Companies.FirstOrDefaultAsync(c => c.Id == company.Id)
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
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task SoftDeleteAsync(Guid companyId)
    {
        var c = await _context.Companies.FindAsync(companyId);
        if (c != null)
        {
            c.IsActive = false;
            c.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public Task<bool> UserOwnsAsync(Guid userId, Guid companyId)
        => _context.Companies.AnyAsync(c => c.Id == companyId && c.OwnerUserId == userId && c.IsActive);
}
