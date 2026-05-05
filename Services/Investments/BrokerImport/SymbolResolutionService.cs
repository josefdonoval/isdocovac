using Isdocovac.Providers.Investments;

namespace Isdocovac.Services.Investments.BrokerImport;

public interface ISymbolResolutionService
{
    Task<Dictionary<string, string>> ResolveAsync(Guid companyId, IEnumerable<string> isins, CancellationToken ct = default);
}

public class SymbolResolutionService : ISymbolResolutionService
{
    private readonly IShareProvider _shareProvider;
    private readonly ISecurityLookupService _securityLookup;
    private readonly ILogger<SymbolResolutionService> _logger;

    public SymbolResolutionService(
        IShareProvider shareProvider,
        ISecurityLookupService securityLookup,
        ILogger<SymbolResolutionService> logger)
    {
        _shareProvider = shareProvider;
        _securityLookup = securityLookup;
        _logger = logger;
    }

    public async Task<Dictionary<string, string>> ResolveAsync(Guid companyId, IEnumerable<string> isins, CancellationToken ct = default)
    {
        var distinctIsins = isins
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (distinctIsins.Count == 0) return result;

        // 1) Reuse what's already in shares for this company.
        var existing = await _shareProvider.GetSymbolByIsinAsync(companyId, distinctIsins);
        foreach (var kv in existing)
        {
            if (!string.IsNullOrWhiteSpace(kv.Value))
            {
                result[kv.Key] = kv.Value;
            }
        }

        // 2) For the rest, try OpenFigi. Fall back to ISIN itself.
        foreach (var isin in distinctIsins)
        {
            if (result.ContainsKey(isin)) continue;

            try
            {
                var hit = await _securityLookup.LookupByIsinAsync(isin, ct);
                if (!string.IsNullOrWhiteSpace(hit?.Ticker))
                {
                    result[isin] = hit!.Ticker!;
                    continue;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Symbol lookup failed for ISIN {Isin}", isin);
            }

            // Fallback: store ISIN itself; user can edit before import.
            result[isin] = isin;
        }

        return result;
    }
}
