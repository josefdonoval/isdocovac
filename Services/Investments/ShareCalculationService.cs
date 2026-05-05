using Isdocovac.Data;
using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Isdocovac.Services.Fx;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Services.Investments;

public record ShareOpenLot(Share OpenTrade, decimal Remaining);

public record ShareOpenPosition(
    string Symbol,
    string Currency,
    IReadOnlyList<Broker> Brokers,
    decimal NetQuantity,
    decimal AvgBuyPrice,
    decimal CostBasisUsd,
    decimal CostBasisCzk,
    IReadOnlyList<ShareOpenLot> Lots);

public interface IShareCalculationService
{
    Task PrepareAsync(Share trade, CancellationToken ct = default);
    Task RecomputeMatchingAsync(Guid companyId, string symbol, CancellationToken ct = default);
    Task RecomputeAllSymbolsAsync(Guid companyId, CancellationToken ct = default);
    Task<IReadOnlyList<ShareOpenPosition>> ComputeOpenPositionsAsync(Guid companyId, CancellationToken ct = default);

    /// <summary>
    /// Net long quantity available to sell for (companyId, symbol). Respects company's FIFO scope
    /// — Global sums across brokers, PerBroker stays within `broker`. `excludeTradeId` lets the edit
    /// form ignore the trade currently being modified so its own pre-edit quantity isn't double counted.
    /// Negative net (shouldn't happen given the no-short guard) is clamped to 0.
    /// </summary>
    Task<decimal> GetAvailableSellQuantityAsync(
        Guid companyId, string symbol, Broker broker,
        Guid? excludeTradeId = null,
        CancellationToken ct = default);
}

public class ShareCalculationService : IShareCalculationService
{
    private readonly ICnbExchangeRateService _cnb;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<ShareCalculationService> _logger;

    public ShareCalculationService(
        ICnbExchangeRateService cnb,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<ShareCalculationService> logger)
    {
        _cnb = cnb;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task PrepareAsync(Share trade, CancellationToken ct = default)
    {
        // Shares: 1:1, no contract multiplier.
        trade.Proceeds = Math.Round(-trade.Quantity * trade.TradePrice, 2, MidpointRounding.AwayFromZero);
        trade.Basis = Math.Round(trade.Proceeds - trade.CommissionFee, 2, MidpointRounding.AwayFromZero);

        // FX from CNB — derive date from user-local TradeDateTime (per CLAUDE.md).
        var date = DateOnly.FromDateTime(trade.TradeDateTime);
        var rate = await _cnb.GetRateAsync(
            string.IsNullOrWhiteSpace(trade.Currency) ? "USD" : trade.Currency,
            date,
            ct);

        trade.CnbDate = rate.Date;
        trade.FxRate = rate.Rate;
        trade.FxAmount = rate.Amount;

        trade.ProceedsCzk = ToCzk(trade.Proceeds, rate);
        trade.CommissionFeeCzk = ToCzk(trade.CommissionFee, rate);
        trade.BasisCzk = ToCzk(trade.Basis, rate);

        trade.RealizedPnl = null;
        trade.RealizedPnlCzk = null;
    }

    public async Task RecomputeMatchingAsync(Guid companyId, string symbol, CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateDbContext();

        var scope = await context.Companies
            .Where(c => c.Id == companyId)
            .Select(c => c.ShareFifoScope)
            .FirstOrDefaultAsync(ct);

        var trades = await context.Shares
            .Where(t => t.CompanyId == companyId && t.Symbol == symbol)
            .OrderBy(t => t.TradeDateTime)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);

        if (scope == ShareFifoScope.PerBroker)
        {
            foreach (var group in trades.GroupBy(t => t.Broker))
            {
                MatchFifo(group.OrderBy(t => t.TradeDateTime).ThenBy(t => t.CreatedAt).ToList(), symbol);
            }
        }
        else
        {
            MatchFifo(trades, symbol);
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task RecomputeAllSymbolsAsync(Guid companyId, CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        var symbols = await context.Shares
            .Where(t => t.CompanyId == companyId)
            .Select(t => t.Symbol)
            .Distinct()
            .ToListAsync(ct);

        foreach (var symbol in symbols)
        {
            await RecomputeMatchingAsync(companyId, symbol, ct);
        }
    }

    public async Task<decimal> GetAvailableSellQuantityAsync(
        Guid companyId, string symbol, Broker broker,
        Guid? excludeTradeId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return 0m;

        await using var context = _contextFactory.CreateDbContext();
        var scope = await context.Companies
            .Where(c => c.Id == companyId)
            .Select(c => c.ShareFifoScope)
            .FirstOrDefaultAsync(ct);

        var trimmed = symbol.Trim();
        var query = context.Shares
            .Where(t => t.CompanyId == companyId && t.Symbol == trimmed);

        if (scope == ShareFifoScope.PerBroker)
        {
            query = query.Where(t => t.Broker == broker);
        }

        if (excludeTradeId.HasValue)
        {
            query = query.Where(t => t.Id != excludeTradeId.Value);
        }

        var net = await query.SumAsync(t => (decimal?)t.Quantity, ct) ?? 0m;
        return net > 0m ? net : 0m;
    }

    public async Task<IReadOnlyList<ShareOpenPosition>> ComputeOpenPositionsAsync(Guid companyId, CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateDbContext();

        var scope = await context.Companies
            .Where(c => c.Id == companyId)
            .Select(c => c.ShareFifoScope)
            .FirstOrDefaultAsync(ct);

        var trades = await context.Shares
            .Where(t => t.CompanyId == companyId)
            .OrderBy(t => t.TradeDateTime)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);

        var positions = new List<ShareOpenPosition>();

        // Symbol is the user-visible aggregation level. Within a symbol, FIFO is applied either
        // globally (one queue) or per broker (one queue per broker) — but the resulting open lots
        // are merged into a single position per symbol for display.
        foreach (var symbolGroup in trades.GroupBy(t => t.Symbol))
        {
            var lots = new List<ShareOpenLot>();

            if (scope == ShareFifoScope.PerBroker)
            {
                foreach (var brokerGroup in symbolGroup.GroupBy(t => t.Broker))
                {
                    lots.AddRange(BuildOpenLots(brokerGroup.OrderBy(t => t.TradeDateTime).ThenBy(t => t.CreatedAt).ToList()));
                }
            }
            else
            {
                lots.AddRange(BuildOpenLots(symbolGroup.OrderBy(t => t.TradeDateTime).ThenBy(t => t.CreatedAt).ToList()));
            }

            if (lots.Count == 0) continue;

            var netQty = lots.Sum(l => l.Remaining);
            if (netQty <= 0) continue;

            // Cost basis from remaining lots: each lot contributes (Basis / Quantity) * Remaining.
            // For a Buy, Basis is negative (cash outflow). We expose absolute cost in the result.
            decimal costBasisUsd = 0m;
            decimal costBasisCzk = 0m;
            decimal weightedPriceNum = 0m;
            foreach (var lot in lots)
            {
                if (lot.OpenTrade.Quantity == 0) continue;
                var basisPerShareUsd = lot.OpenTrade.Basis / lot.OpenTrade.Quantity;
                var basisPerShareCzk = lot.OpenTrade.BasisCzk / lot.OpenTrade.Quantity;
                costBasisUsd += basisPerShareUsd * lot.Remaining;
                costBasisCzk += basisPerShareCzk * lot.Remaining;
                weightedPriceNum += lot.OpenTrade.TradePrice * lot.Remaining;
            }

            var avgBuyPrice = netQty > 0 ? weightedPriceNum / netQty : 0m;
            var brokers = lots.Select(l => l.OpenTrade.Broker).Distinct().OrderBy(b => (int)b).ToList();
            var currency = lots.First().OpenTrade.Currency;

            positions.Add(new ShareOpenPosition(
                Symbol: symbolGroup.Key,
                Currency: currency,
                Brokers: brokers,
                NetQuantity: netQty,
                AvgBuyPrice: Math.Round(avgBuyPrice, 6, MidpointRounding.AwayFromZero),
                CostBasisUsd: Math.Round(Math.Abs(costBasisUsd), 2, MidpointRounding.AwayFromZero),
                CostBasisCzk: Math.Round(Math.Abs(costBasisCzk), 2, MidpointRounding.AwayFromZero),
                Lots: lots));
        }

        return positions.OrderBy(p => p.Symbol).ToList();
    }

    // FIFO matching for a single ordered group of trades. Mutates entities (RealizedPnl + UpdatedAt).
    private void MatchFifo(IReadOnlyList<Share> trades, string symbol)
    {
        var openQueue = new Queue<(Share Open, decimal Remaining)>();

        foreach (var t in trades)
        {
            if (t.Code == ShareTradeCode.Buy)
            {
                if (t.Quantity > 0)
                {
                    openQueue.Enqueue((t, t.Quantity));
                }
                else
                {
                    _logger.LogWarning(
                        "Non-positive quantity on Buy for {Symbol} at {Date} — skipped from FIFO queue.",
                        symbol, t.TradeDateTime);
                }
                t.RealizedPnl = null;
                t.RealizedPnlCzk = null;
                continue;
            }

            // Sell — match FIFO
            var sellQty = Math.Abs(t.Quantity);
            decimal matchedOpenBasisUsd = 0m;
            decimal matched = 0m;

            while (matched < sellQty && openQueue.Count > 0)
            {
                var (openTrade, remaining) = openQueue.Dequeue();
                var take = Math.Min(remaining, sellQty - matched);

                if (openTrade.Quantity != 0)
                {
                    var basisPerShareUsd = openTrade.Basis / openTrade.Quantity;
                    matchedOpenBasisUsd += basisPerShareUsd * take;
                }
                matched += take;

                if (take < remaining)
                {
                    openQueue.Enqueue((openTrade, remaining - take));
                }
            }

            if (matched < sellQty)
            {
                t.RealizedPnl = null;
                t.RealizedPnlCzk = null;
                _logger.LogWarning(
                    "Sell trade {Id} for {Symbol} could not be fully matched ({Matched}/{Required}).",
                    t.Id, symbol, matched, sellQty);
                continue;
            }

            var sellBasisPerShareUsd = sellQty == 0 ? 0m : t.Basis / sellQty;
            var pnlUsd = Math.Round(sellBasisPerShareUsd * matched + matchedOpenBasisUsd,
                2, MidpointRounding.AwayFromZero);
            t.RealizedPnl = pnlUsd;

            if (t.FxAmount <= 0)
            {
                t.RealizedPnlCzk = null;
            }
            else
            {
                t.RealizedPnlCzk = Math.Round(
                    pnlUsd * t.FxRate / t.FxAmount, 2, MidpointRounding.AwayFromZero);
            }

            t.UpdatedAt = DateTime.UtcNow;
        }
    }

    // Same FIFO walk as MatchFifo, but returns remaining open lots instead of writing P/L.
    private static List<ShareOpenLot> BuildOpenLots(IReadOnlyList<Share> trades)
    {
        var openQueue = new Queue<(Share Open, decimal Remaining)>();

        foreach (var t in trades)
        {
            if (t.Code == ShareTradeCode.Buy)
            {
                if (t.Quantity > 0)
                {
                    openQueue.Enqueue((t, t.Quantity));
                }
                continue;
            }

            var sellQty = Math.Abs(t.Quantity);
            decimal matched = 0m;

            while (matched < sellQty && openQueue.Count > 0)
            {
                var (openTrade, remaining) = openQueue.Dequeue();
                var take = Math.Min(remaining, sellQty - matched);
                matched += take;
                if (take < remaining)
                {
                    openQueue.Enqueue((openTrade, remaining - take));
                }
            }
        }

        return openQueue.Select(x => new ShareOpenLot(x.Open, x.Remaining)).ToList();
    }

    private static decimal ToCzk(decimal amount, FxRate rate)
    {
        if (rate.Amount <= 0) return 0m;
        return Math.Round(amount * rate.Rate / rate.Amount, 2, MidpointRounding.AwayFromZero);
    }
}
