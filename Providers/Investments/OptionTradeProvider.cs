using Isdocovac.Data;
using Isdocovac.Models;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Providers.Investments;

public interface IOptionTradeProvider
{
    Task<IReadOnlyList<int>> ListYearsAsync(Guid companyId);
    Task<IReadOnlyList<OptionTrade>> ListByYearAsync(Guid companyId, int year);
    Task<IReadOnlyList<OptionTrade>> ListBySymbolAsync(Guid companyId, string symbol);
    Task<OptionTrade?> GetAsync(Guid id);
    Task<OptionTrade> CreateAsync(OptionTrade trade);
    Task<OptionTrade> UpdateAsync(OptionTrade trade);
    Task DeleteAsync(Guid id);
    Task<int> CreateBulkAsync(IEnumerable<OptionTrade> trades);
}

public class OptionTradeProvider : IOptionTradeProvider
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public OptionTradeProvider(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<int>> ListYearsAsync(Guid companyId)
    {
        await using var context = _contextFactory.CreateDbContext();
        var years = await context.OptionTrades
            .Where(t => t.CompanyId == companyId)
            .Select(t => t.TradeDateTime.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToListAsync();
        return years;
    }

    public async Task<IReadOnlyList<OptionTrade>> ListByYearAsync(Guid companyId, int year)
    {
        var start = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await using var context = _contextFactory.CreateDbContext();
        return await context.OptionTrades
            .Where(t => t.CompanyId == companyId && t.TradeDateTime >= start && t.TradeDateTime < end)
            .OrderBy(t => t.TradeDateTime)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<OptionTrade>> ListBySymbolAsync(Guid companyId, string symbol)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.OptionTrades
            .Where(t => t.CompanyId == companyId && t.Symbol == symbol)
            .OrderBy(t => t.TradeDateTime)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<OptionTrade?> GetAsync(Guid id)
    {
        await using var context = _contextFactory.CreateDbContext();
        return await context.OptionTrades.FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<OptionTrade> CreateAsync(OptionTrade trade)
    {
        var now = DateTime.UtcNow;
        trade.Id = trade.Id == Guid.Empty ? Guid.NewGuid() : trade.Id;
        trade.CreatedAt = now;
        trade.UpdatedAt = now;

        await using var context = _contextFactory.CreateDbContext();
        context.OptionTrades.Add(trade);
        await context.SaveChangesAsync();
        return trade;
    }

    public async Task<OptionTrade> UpdateAsync(OptionTrade trade)
    {
        await using var context = _contextFactory.CreateDbContext();
        var existing = await context.OptionTrades.FirstOrDefaultAsync(t => t.Id == trade.Id)
            ?? throw new InvalidOperationException($"OptionTrade {trade.Id} not found.");

        existing.Symbol = trade.Symbol;
        existing.TradeDateTime = trade.TradeDateTime;
        existing.Quantity = trade.Quantity;
        existing.TradePrice = trade.TradePrice;
        existing.Currency = trade.Currency;
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
        var existing = await context.OptionTrades.FirstOrDefaultAsync(t => t.Id == id);
        if (existing != null)
        {
            context.OptionTrades.Remove(existing);
            await context.SaveChangesAsync();
        }
    }

    public async Task<int> CreateBulkAsync(IEnumerable<OptionTrade> trades)
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
        context.OptionTrades.AddRange(list);
        await context.SaveChangesAsync();
        return list.Count;
    }
}
