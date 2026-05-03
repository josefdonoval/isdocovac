using Isdocovac.Data;
using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers;

public interface IContactProvider
{
    Task<IEnumerable<Contact>> GetByKindAsync(Guid companyId, ContactKind kind);
    Task<IReadOnlyList<Contact>> ListDirectoryAsync(Guid companyId);
    Task<Contact?> GetByIdAsync(Guid id);
    Task<Contact> AddAsync(Contact contact);
    Task<Contact> UpdateAsync(Contact contact);
    Task DeleteAsync(Guid id);
}

public class ContactProvider : IContactProvider
{
    private readonly ApplicationDbContext _context;

    public ContactProvider(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Contact>> GetByKindAsync(Guid companyId, ContactKind kind)
    {
        return await _context.Contacts
            .Where(c => c.CompanyId == companyId && c.Kind == kind)
            .OrderBy(c => c.CompanyName)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Contact>> ListDirectoryAsync(Guid companyId)
    {
        return await _context.Contacts
            .Where(c => c.CompanyId == companyId)
            .OrderBy(c => c.CompanyName ?? (c.LastName + " " + c.FirstName))
            .ToListAsync();
    }

    public async Task<Contact> AddAsync(Contact contact)
    {
        var now = DateTime.UtcNow;
        contact.Id = contact.Id == Guid.Empty ? Guid.NewGuid() : contact.Id;
        contact.CreatedAt = now;
        contact.UpdatedAt = now;
        _context.Contacts.Add(contact);
        await _context.SaveChangesAsync();
        return contact;
    }

    public async Task<Contact> UpdateAsync(Contact contact)
    {
        var existing = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contact.Id)
            ?? throw new InvalidOperationException($"Contact {contact.Id} not found.");

        existing.Kind = contact.Kind;
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
        existing.IsActive = contact.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
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
