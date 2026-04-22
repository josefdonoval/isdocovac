using System.Globalization;

namespace Isdocovac.Services.Vat;

public static class VatXmlFormat
{
    /// Fakturoid-style decimal: invariant culture, trailing zero ".0" required when integer.
    public static string Decimal(decimal value)
    {
        var rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        if (rounded == Math.Truncate(rounded))
        {
            return rounded.ToString("0.0", CultureInfo.InvariantCulture);
        }
        return rounded.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// DPHDP3 Veta1/Veta4/Veta6 — whole-CZK rounding (per Fakturoid sample), formatted with ".0".
    public static string DecimalWholeCzk(decimal value)
    {
        var rounded = Math.Round(value, 0, MidpointRounding.AwayFromZero);
        return rounded.ToString("0.0", CultureInfo.InvariantCulture);
    }

    public static string Date(DateTime date)
    {
        return date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }

    public static string Date(DateOnly date)
    {
        return date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }
}
