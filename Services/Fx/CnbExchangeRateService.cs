using System.Globalization;
using System.Text.Json;
using Isdocovac.Models;
using Isdocovac.Providers.Fx;

namespace Isdocovac.Services.Fx;

public interface ICnbExchangeRateService
{
    Task<decimal> ConvertToCzkAsync(decimal amount, string currency, DateOnly date, CancellationToken ct = default);
    Task<FxRate> GetRateAsync(string currency, DateOnly date, CancellationToken ct = default);
}

public class CnbExchangeRateService : ICnbExchangeRateService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFxRateProvider _fxProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CnbExchangeRateService> _logger;

    public CnbExchangeRateService(
        IHttpClientFactory httpClientFactory,
        IFxRateProvider fxProvider,
        IConfiguration configuration,
        ILogger<CnbExchangeRateService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _fxProvider = fxProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<decimal> ConvertToCzkAsync(decimal amount, string currency, DateOnly date, CancellationToken ct = default)
    {
        if (string.Equals(currency, "CZK", StringComparison.OrdinalIgnoreCase))
        {
            return amount;
        }

        var rate = await GetRateAsync(currency, date, ct);
        return Math.Round(amount * rate.Rate / rate.Amount, 2, MidpointRounding.AwayFromZero);
    }

    public async Task<FxRate> GetRateAsync(string currency, DateOnly date, CancellationToken ct = default)
    {
        var code = currency.ToUpperInvariant();

        var cached = await _fxProvider.GetAsync(date, code);
        if (cached != null)
        {
            return cached;
        }

        var all = await FetchDailyRatesAsync(date, ct);
        FxRate? target = null;
        foreach (var r in all)
        {
            await _fxProvider.UpsertAsync(r);
            if (r.Currency == code)
            {
                target = r;
            }
        }

        if (target == null)
        {
            throw new InvalidOperationException(
                $"CNB did not return a rate for {code} on {date:yyyy-MM-dd}.");
        }

        return target;
    }

    private async Task<List<FxRate>> FetchDailyRatesAsync(DateOnly date, CancellationToken ct)
    {
        var baseUrl = _configuration["Cnb:BaseUrl"] ?? "https://api.cnb.cz";
        var url = $"{baseUrl}/cnbapi/exrates/daily?date={date:yyyy-MM-dd}&lang=EN";

        var httpClient = _httpClientFactory.CreateClient("Cnb");
        var response = await httpClient.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("CNB API request failed {Status}: {Body}", response.StatusCode, body);
            throw new HttpRequestException($"CNB API request failed: {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("rates", out var ratesEl))
        {
            throw new InvalidOperationException("CNB response missing 'rates' property.");
        }

        var result = new List<FxRate>();
        foreach (var el in ratesEl.EnumerateArray())
        {
            var currencyCode = el.GetProperty("currencyCode").GetString();
            var amount = el.GetProperty("amount").GetInt32();
            var rate = el.GetProperty("rate").GetDecimal();
            var validFor = el.TryGetProperty("validFor", out var vf)
                ? DateOnly.ParseExact(vf.GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                : date;

            if (string.IsNullOrWhiteSpace(currencyCode))
            {
                continue;
            }

            result.Add(new FxRate
            {
                Date = date,
                Currency = currencyCode.ToUpperInvariant(),
                Amount = amount,
                Rate = rate,
            });
        }

        return result;
    }
}
