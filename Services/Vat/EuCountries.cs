namespace Isdocovac.Services.Vat;

public static class EuCountries
{
    private static readonly HashSet<string> _iso2 = new(StringComparer.OrdinalIgnoreCase)
    {
        "AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "ES", "FI",
        "FR", "GR", "EL", "HR", "HU", "IE", "IT", "LT", "LU", "LV",
        "MT", "NL", "PL", "PT", "RO", "SE", "SI", "SK",
    };

    public static bool IsEu(string? countryCode)
    {
        return !string.IsNullOrWhiteSpace(countryCode) && _iso2.Contains(countryCode);
    }
}
