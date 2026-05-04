using Isdocovac.Data;
using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Isdocovac.Services.Fx;
using Microsoft.EntityFrameworkCore;

namespace Isdocovac.Services.Investments;

public interface IOptionTradeCalculationService
{
    Task PrepareAsync(OptionTrade trade, CancellationToken ct = default);
    Task RecomputeMatchingAsync(Guid companyId, string symbol, CancellationToken ct = default);
}

public class OptionTradeCalculationService : IOptionTradeCalculationService
{
    private readonly ICnbExchangeRateService _cnb;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<OptionTradeCalculationService> _logger;

    public OptionTradeCalculationService(
        ICnbExchangeRateService cnb,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<OptionTradeCalculationService> logger)
    {
        _cnb = cnb;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task PrepareAsync(OptionTrade trade, CancellationToken ct = default)
    {
        // Trade currency (USD) — deterministic. Standard US equity option multiplier = 100.
        const int contractMultiplier = 100;
        trade.Proceeds = Math.Round(-(decimal)trade.Quantity * trade.TradePrice * contractMultiplier,
            2, MidpointRounding.AwayFromZero);
        trade.Basis = Math.Round(trade.Proceeds - trade.CommissionFee, 2, MidpointRounding.AwayFromZero);

        // FX from CNB
        var date = DateOnly.FromDateTime(trade.TradeDateTime);
        var rate = await _cnb.GetRateAsync(
            string.IsNullOrWhiteSpace(trade.Currency) ? "USD" : trade.Currency,
            date,
            ct);

        trade.CnbDate = rate.Date;
        trade.FxRate = rate.Rate;
        trade.FxAmount = rate.Amount;

        // CZK columns — per-leg conversion at trade date rate
        trade.ProceedsCzk = ToCzk(trade.Proceeds, rate);
        trade.CommissionFeeCzk = ToCzk(trade.CommissionFee, rate);
        trade.BasisCzk = ToCzk(trade.Basis, rate);

        // Realized P/L is computed only by RecomputeMatchingAsync (FIFO)
        trade.RealizedPnl = null;
        trade.RealizedPnlCzk = null;
    }

    public async Task RecomputeMatchingAsync(Guid companyId, string symbol, CancellationToken ct = default)
    {
        await using var context = _contextFactory.CreateDbContext();

        var trades = await context.OptionTrades
            .Where(t => t.CompanyId == companyId && t.Symbol == symbol)
            .OrderBy(t => t.TradeDateTime)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(ct);

        // FIFO queue of open contracts: (trade, contractsRemaining, basisPerContract)
        var openQueue = new Queue<(OptionTrade Open, int Remaining)>();

        foreach (var t in trades)
        {
            if (t.Code == OptionTradeCode.Open)
            {
                // v1: only long opens (Buy to Open). Short opens are tracked but not matched.
                if (t.Quantity > 0)
                {
                    openQueue.Enqueue((t, t.Quantity));
                }
                else
                {
                    _logger.LogWarning(
                        "Short open detected for {Symbol} on {Date} — FIFO matching skipped (v1 supports long-only).",
                        symbol, t.TradeDateTime);
                }
                t.RealizedPnl = null;
                t.RealizedPnlCzk = null;
                continue;
            }

            // Close trade — match FIFO
            var closeQty = Math.Abs(t.Quantity);
            decimal matchedOpenBasisUsd = 0m;
            int matched = 0;

            while (matched < closeQty && openQueue.Count > 0)
            {
                var (openTrade, remaining) = openQueue.Dequeue();
                int take = Math.Min(remaining, closeQty - matched);

                var basisPerContractUsd = openTrade.Basis / openTrade.Quantity; // negative for long open
                matchedOpenBasisUsd += basisPerContractUsd * take;
                matched += take;

                if (take < remaining)
                {
                    openQueue.Enqueue((openTrade, remaining - take));
                }
            }

            if (matched < closeQty)
            {
                t.RealizedPnl = null;
                t.RealizedPnlCzk = null;
                _logger.LogWarning(
                    "Close trade {Id} for {Symbol} could not be fully matched ({Matched}/{Required}).",
                    t.Id, symbol, matched, closeQty);
                continue;
            }

            var closeBasisPerContractUsd = t.Basis / closeQty; // positive for sell-to-close
            var pnlUsd = Math.Round(closeBasisPerContractUsd * matched + matchedOpenBasisUsd,
                2, MidpointRounding.AwayFromZero);
            t.RealizedPnl = pnlUsd;

            // CZK P/L follows the close-trade rate convention (matches typical broker statements).
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

        await context.SaveChangesAsync(ct);
    }

    private static decimal ToCzk(decimal amount, FxRate rate)
    {
        if (rate.Amount <= 0) return 0m;
        return Math.Round(amount * rate.Rate / rate.Amount, 2, MidpointRounding.AwayFromZero);
    }
}
