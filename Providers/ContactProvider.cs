using Isdocovac.Data;
using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers;

public interface IContactProvider
{
    Task<Contact?> GetOwnCompanyAsync(Guid userId);
    Task<Contact> UpsertOwnCompanyAsync(Contact contact);
    Task<IEnumerable<Contact>> GetByKindAsync(Guid userId, ContactKind kind);
    Task<Contact?> GetByIdAsync(Guid id);
    Task DeleteAsync(Guid id);
}

public class ContactProvider : IContactProvider
{
    private readonly ApplicationDbContext _context;

    public ContactProvider(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Contact?> GetOwnCompanyAsync(Guid userId)
    {
        return _context.Contacts
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Kind == ContactKind.OwnCompany);
    }

    public async Task<Contact> UpsertOwnCompanyAsync(Contact contact)
    {
        var existing = await GetOwnCompanyAsync(contact.UserId);
        var now = DateTime.UtcNow;

        if (existing == null)
        {
            contact.Id = contact.Id == Guid.Empty ? Guid.NewGuid() : contact.Id;
            contact.Kind = ContactKind.OwnCompany;
            contact.CreatedAt = now;
            contact.UpdatedAt = now;
            _context.Contacts.Add(contact);
        }
        else
        {
            existing.CompanyName = contact.CompanyName;
            existing.FirstName = contact.FirstName;
            existing.LastName = contact.LastName;
            existing.Titul = contact.Titul;
            existing.IsLegalEntity = contact.IsLegalEntity;
            existing.Dic = contact.Dic;
            existing.Ico = contact.Ico;
            existing.Street = contact.Street;
            existing.HouseNumber = contact.HouseNumber;
            existing.OrientNumber = contact.OrientNumber;
            existing.City = contact.City;
            existing.Zip = contact.Zip;
            existing.CountryCode = contact.CountryCode;
            existing.Email = contact.Email;
            existing.Phone = contact.Phone;
            existing.TaxOfficeCode = contact.TaxOfficeCode;
            existing.TaxOfficeBranchCode = contact.TaxOfficeBranchCode;
            existing.OkecCode = contact.OkecCode;
            existing.DataBoxId = contact.DataBoxId;
            existing.IsActive = contact.IsActive;
            existing.UpdatedAt = now;
            contact = existing;
        }

        await _context.SaveChangesAsync();
        return contact;
    }

    public async Task<IEnumerable<Contact>> GetByKindAsync(Guid userId, ContactKind kind)
    {
        return await _context.Contacts
            .Where(c => c.UserId == userId && c.Kind == kind)
            .OrderBy(c => c.CompanyName)
            .ToListAsync();
    }

    public Task<Contact?> GetByIdAsync(Guid id)
    {
        return _context.Contacts.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task DeleteAsync(Guid id)
    {
        var c = await _context.Contacts.FindAsync(id);
        if (c != null)
        {
            _context.Contacts.Remove(c);
            await _context.SaveChangesAsync();
        }
    }
}
