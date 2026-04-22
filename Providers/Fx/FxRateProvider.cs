using Isdocovac.Data;
using Isdocovac.Models;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers.Fx;

public interface IFxRateProvider
{
    Task<FxRate?> GetAsync(DateOnly date, string currency);
    Task<FxRate> UpsertAsync(FxRate rate);
}

public class FxRateProvider : IFxRateProvider
{
    private readonly ApplicationDbContext _context;

    public FxRateProvider(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<FxRate?> GetAsync(DateOnly date, string currency)
    {
        var code = currency.ToUpperInvariant();
        return _context.FxRates
            .FirstOrDefaultAsync(r => r.Date == date && r.Currency == code);
    }

    public async Task<FxRate> UpsertAsync(FxRate rate)
    {
        rate.Currency = rate.Currency.ToUpperInvariant();
        var existing = await GetAsync(rate.Date, rate.Currency);
        if (existing == null)
        {
            rate.Id = rate.Id == Guid.Empty ? Guid.NewGuid() : rate.Id;
            rate.FetchedAt = DateTime.UtcNow;
            _context.FxRates.Add(rate);
        }
        else
        {
            existing.Amount = rate.Amount;
            existing.Rate = rate.Rate;
            existing.FetchedAt = DateTime.UtcNow;
            rate = existing;
        }
        await _context.SaveChangesAsync();
        return rate;
    }
}
