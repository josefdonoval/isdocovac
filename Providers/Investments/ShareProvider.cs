using Isdocovac.Data;
using Isdocovac.Models;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers.Investments;

public interface IShareProvider
{
    Task<IReadOnlyList<int>> ListYearsAsync(Guid companyId);
    Task<IReadOnlyList<Share>> ListByYearAsync(Guid companyId, int year);
    Task<IReadOnlyList<Share>> ListBySymbolAsync(Guid companyId, string symbol);
    Task<IReadOnlyList<Share>> ListAllAsync(Guid companyId);
    Task<Share?> GetAsync(Guid id);
    Task<Share> CreateAsync(Share trade);
    Task<Share> UpdateAsync(Share trade);
    Task DeleteAsync(Guid id);
    Task<int> CreateBulkAsync(IEnumerable<Share> trades);
}

public class ShareProvider : IShareProvider
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public ShareProvider(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<int>> ListYearsAsync(Guid companyId)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.Shares
            .Where(t => t.CompanyId == companyId)
            .Select(t => t.TradeDateTime.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Share>> ListByYearAsync(Guid companyId, int year)
    {
        var start = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await using var context = _contextFactory.CreateDbContext();
        return await context.Shares
            .Where(t => t.CompanyId == companyId && t.TradeDateTime >= start && t.TradeDateTime < end)
            .OrderBy(t => t.TradeDateTime)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Share>> ListBySymbolAsync(Guid companyId, string symbol)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.Shares
            .Where(t => t.CompanyId == companyId && t.Symbol == symbol)
            .OrderBy(t => t.TradeDateTime)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Share>> ListAllAsync(Guid companyId)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.Shares
            .Where(t => t.CompanyId == companyId)
            .OrderBy(t => t.TradeDateTime)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<Share?> GetAsync(Guid id)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.Shares.FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Share> CreateAsync(Share trade)
    {
        var now = DateTime.UtcNow;
        trade.Id = trade.Id == Guid.Empty ? Guid.NewGuid() : trade.Id;
        trade.CreatedAt = now;
        trade.UpdatedAt = now;

        await using var context = _contextFactory.CreateDbContext();
        context.Shares.Add(trade);
        await context.SaveChangesAsync();
        return trade;
    }

    public async Task<Share> UpdateAsync(Share trade)
    {
        await using var context = _contextFactory.CreateDbContext();
        var existing = await context.Shares.FirstOrDefaultAsync(t => t.Id == trade.Id)
            ?? throw new InvalidOperationException($"Share {trade.Id} not found.");

        existing.Symbol = trade.Symbol;
        existing.Isin = trade.Isin;
        existing.Name = trade.Name;
        existing.TradeDateTime = trade.TradeDateTime;
        existing.Quantity = trade.Quantity;
        existing.TradePrice = trade.TradePrice;
        existing.Currency = trade.Currency;
        existing.Broker = trade.Broker;
        existing.Proceeds = trade.Proceeds;
        existing.CommissionFee = trade.CommissionFee;
        existing.Basis = trade.Basis;
        existing.RealizedPnl = trade.RealizedPnl;
        existing.Code = trade.Code;
        existing.CnbDate = trade.CnbDate;
        existing.FxRate = trade.FxRate;
        existing.FxAmount = trade.FxAmount;
        existing.ProceedsCzk = trade.ProceedsCzk;
        existing.CommissionFeeCzk = trade.CommissionFeeCzk;
        existing.BasisCzk = trade.BasisCzk;
        existing.RealizedPnlCzk = trade.RealizedPnlCzk;
        existing.Notes = trade.Notes;
        existing.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var context = _contextFactory.CreateDbContext();
        var existing = await context.Shares.FirstOrDefaultAsync(t => t.Id == id);
        if (existing != null)
        {
            context.Shares.Remove(existing);
            await context.SaveChangesAsync();
        }
    }

    public async Task<int> CreateBulkAsync(IEnumerable<Share> trades)
    {
        var now = DateTime.UtcNow;
        var list = trades.ToList();
        foreach (var t in list)
        {
            t.Id = t.Id == Guid.Empty ? Guid.NewGuid() : t.Id;
            t.CreatedAt = now;
            t.UpdatedAt = now;
        }

        await using var context = _contextFactory.CreateDbContext();
        context.Shares.AddRange(list);
        await context.SaveChangesAsync();
        return list.Count;
    }
}
