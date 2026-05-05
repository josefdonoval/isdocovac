using Isdocovac.Data;
using Isdocovac.Models;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers.Investments;

public interface IShareQuoteProvider
{
    Task<ShareQuote?> GetAsync(Guid companyId, string symbol);
    Task<IReadOnlyList<ShareQuote>> ListAsync(Guid companyId);
    Task<ShareQuote> UpsertAsync(ShareQuote quote);
}

public class ShareQuoteProvider : IShareQuoteProvider
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public ShareQuoteProvider(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<ShareQuote?> GetAsync(Guid companyId, string symbol)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.ShareQuotes
            .FirstOrDefaultAsync(q => q.CompanyId == companyId && q.Symbol == symbol);
    }

    public async Task<IReadOnlyList<ShareQuote>> ListAsync(Guid companyId)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.ShareQuotes
            .Where(q => q.CompanyId == companyId)
            .ToListAsync();
    }

    public async Task<ShareQuote> UpsertAsync(ShareQuote quote)
    {
        await using var context = _contextFactory.CreateDbContext();
        var existing = await context.ShareQuotes
            .FirstOrDefaultAsync(q => q.CompanyId == quote.CompanyId && q.Symbol == quote.Symbol);

        if (existing == null)
        {
            quote.Id = quote.Id == Guid.Empty ? Guid.NewGuid() : quote.Id;
            context.ShareQuotes.Add(quote);
            await context.SaveChangesAsync();
            return quote;
        }

        existing.Currency = quote.Currency;
        existing.LastPrice = quote.LastPrice;
        existing.FetchedAt = quote.FetchedAt;
        existing.Source = quote.Source;
        // Don't overwrite a known Name with null — Name comes from a separate ISIN lookup.
        if (!string.IsNullOrWhiteSpace(quote.Name))
        {
            existing.Name = quote.Name;
        }
        await context.SaveChangesAsync();
        return existing;
    }
}
