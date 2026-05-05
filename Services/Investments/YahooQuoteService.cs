using System.Globalization;
using System.Text.Json;
using Isdocovac.Models;
using Isdocovac.Providers.Investments;

namespace Isdocovac.Services.Investments;

public interface IShareQuoteService
{
    Task<ShareQuote?> RefreshAsync(Guid companyId, string symbol, CancellationToken ct = default);
    Task<int> RefreshAllOpenAsync(Guid companyId, CancellationToken ct = default);
}

public class YahooQuoteService : IShareQuoteService
{
    private const string Source = "yahoo";

    private readonly IHttpClientFactory _httpFactory;
    private readonly IShareQuoteProvider _quoteProvider;
    private readonly IShareCalculationService _calc;
    private readonly ILogger<YahooQuoteService> _logger;

    public YahooQuoteService(
        IHttpClientFactory httpFactory,
        IShareQuoteProvider quoteProvider,
        IShareCalculationService calc,
        ILogger<YahooQuoteService> logger)
    {
        _httpFactory = httpFactory;
        _quoteProvider = quoteProvider;
        _calc = calc;
        _logger = logger;
    }

    public async Task<ShareQuote?> RefreshAsync(Guid companyId, string symbol, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return null;

        var trimmed = symbol.Trim();
        try
        {
            var (price, currency) = await FetchAsync(trimmed, ct);
            if (price == null) return null;

            var quote = new ShareQuote
            {
                CompanyId = companyId,
                Symbol = trimmed,
                Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency!.ToUpperInvariant(),
                LastPrice = price.Value,
                FetchedAt = DateTime.UtcNow,
                Source = Source,
            };
            return await _quoteProvider.UpsertAsync(quote);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh Yahoo quote for {Symbol}", trimmed);
            return null;
        }
    }

    public async Task<int> RefreshAllOpenAsync(Guid companyId, CancellationToken ct = default)
    {
        var positions = await _calc.ComputeOpenPositionsAsync(companyId, ct);
        var refreshed = 0;
        foreach (var pos in positions)
        {
            var quote = await RefreshAsync(companyId, pos.Symbol, ct);
            if (quote != null) refreshed++;
        }
        return refreshed;
    }

    private async Task<(decimal? Price, string? Currency)> FetchAsync(string symbol, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient("Yahoo");
        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?interval=1d&range=1d";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        // Yahoo blocks default .NET UA on some endpoints — set a browser-like UA.
        req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (compatible; IsdocovacBot/1.0)");
        req.Headers.Accept.ParseAdd("application/json");

        using var resp = await http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogInformation("Yahoo quote HTTP {Status} for {Symbol}", (int)resp.StatusCode, symbol);
            return (null, null);
        }

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var root = doc.RootElement;
        if (!root.TryGetProperty("chart", out var chart)) return (null, null);
        if (!chart.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0)
        {
            return (null, null);
        }

        var first = result[0];
        if (!first.TryGetProperty("meta", out var meta)) return (null, null);

        decimal? price = null;
        if (meta.TryGetProperty("regularMarketPrice", out var pe) && pe.ValueKind == JsonValueKind.Number)
        {
            price = pe.GetDecimal();
        }

        string? currency = null;
        if (meta.TryGetProperty("currency", out var ce) && ce.ValueKind == JsonValueKind.String)
        {
            currency = ce.GetString();
        }

        return (price, currency);
    }
}
