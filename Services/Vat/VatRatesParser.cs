using System.Text.Json;

namespace Isdocovac.Services.Vat;

public record VatRateLine(decimal VatRate, decimal Base, decimal Vat);

public static class VatRatesParser
{
    public static IReadOnlyList<VatRateLine> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<VatRateLine>();
        }

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<VatRateLine>();
        }

        var result = new List<VatRateLine>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object)
            {
                continue;
            }
            var rate = ReadDecimal(el, "vat_rate", "VatRate", "vatRate");
            var baseAmt = ReadDecimal(el, "base", "Base");
            var vat = ReadDecimal(el, "vat", "Vat");
            result.Add(new VatRateLine(rate, baseAmt, vat));
        }
        return result;
    }

    private static decimal ReadDecimal(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out var v))
            {
                return v.ValueKind switch
                {
                    JsonValueKind.Number => v.GetDecimal(),
                    JsonValueKind.String when decimal.TryParse(v.GetString(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var d) => d,
                    _ => 0m
                };
            }
        }
        return 0m;
    }
}
