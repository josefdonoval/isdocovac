using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Isdocovac.Services.Investments;

public record SecurityLookupResult(string Isin, string? Ticker, string? Name, string? ExchangeCode, string? SecurityType);

public interface ISecurityLookupService
{
    Task<SecurityLookupResult?> LookupByIsinAsync(string isin, CancellationToken ct = default);
    bool IsValidIsin(string candidate);
}

public class OpenFigiSecurityLookupService : ISecurityLookupService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<OpenFigiSecurityLookupService> _logger;

    public OpenFigiSecurityLookupService(IHttpClientFactory httpFactory, ILogger<OpenFigiSecurityLookupService> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public bool IsValidIsin(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        var s = candidate.Trim().ToUpperInvariant();
        if (s.Length != 12) return false;
        if (!char.IsLetter(s[0]) || !char.IsLetter(s[1])) return false;
        for (int i = 2; i < 11; i++)
        {
            if (!char.IsLetterOrDigit(s[i])) return false;
        }
        if (!char.IsDigit(s[11])) return false;
        return ChecksumValid(s);
    }

    public async Task<SecurityLookupResult?> LookupByIsinAsync(string isin, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(isin)) return null;
        var normalized = isin.Trim().ToUpperInvariant();
        if (!IsValidIsin(normalized))
        {
            _logger.LogInformation("Rejecting ISIN lookup — invalid format/checksum: {Isin}", normalized);
            return null;
        }

        try
        {
            var http = _httpFactory.CreateClient("OpenFigi");
            using var resp = await http.PostAsJsonAsync(
                "https://api.openfigi.com/v3/mapping",
                new[] { new OpenFigiRequest { IdType = "ID_ISIN", IdValue = normalized } },
                ct);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogInformation("OpenFIGI HTTP {Status} for {Isin}", (int)resp.StatusCode, normalized);
                return null;
            }

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0) return null;

            var first = root[0];
            if (!first.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
            {
                if (first.TryGetProperty("warning", out var warn))
                {
                    _logger.LogInformation("OpenFIGI no data for {Isin}: {Warning}", normalized, warn.GetString());
                }
                return null;
            }

            // Prefer Common Stock + a major exchange; otherwise first entry.
            JsonElement? best = null;
            foreach (var entry in data.EnumerateArray())
            {
                if (entry.TryGetProperty("securityType", out var st) &&
                    st.ValueKind == JsonValueKind.String &&
                    string.Equals(st.GetString(), "Common Stock", StringComparison.OrdinalIgnoreCase))
                {
                    best = entry;
                    break;
                }
            }
            best ??= data[0];

            string? ticker = ReadString(best.Value, "ticker");
            string? name = ReadString(best.Value, "name") ?? ReadString(best.Value, "securityDescription");
            string? exch = ReadString(best.Value, "exchCode");
            string? type = ReadString(best.Value, "securityType");

            return new SecurityLookupResult(normalized, ticker, name, exch, type);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenFIGI lookup failed for {Isin}", normalized);
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var v)) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    // ISO 6166 Luhn-like checksum: convert letters to two digits (A=10), apply double-every-other-from-right, sum, mod 10 == 0.
    private static bool ChecksumValid(string isin)
    {
        var digits = new List<int>(24);
        foreach (var ch in isin)
        {
            if (char.IsDigit(ch))
            {
                digits.Add(ch - '0');
            }
            else
            {
                var v = ch - 'A' + 10;
                digits.Add(v / 10);
                digits.Add(v % 10);
            }
        }

        int sum = 0;
        // Double every other digit starting from the rightmost — i.e. odd positions counted from the right.
        for (int i = 0; i < digits.Count; i++)
        {
            int idxFromRight = digits.Count - 1 - i;
            int d = digits[i];
            if (idxFromRight % 2 == 1)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
        }
        return sum % 10 == 0;
    }

    private class OpenFigiRequest
    {
        [JsonPropertyName("idType")] public string IdType { get; set; } = "";
        [JsonPropertyName("idValue")] public string IdValue { get; set; } = "";
    }
}
