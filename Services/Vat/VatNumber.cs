using System.Text.RegularExpressions;

namespace Isdocovac.Services.Vat;

public static class VatNumber
{
    private static readonly Regex _prefix = new("^[A-Za-z]{2}", RegexOptions.Compiled);

    public static string? StripCountryPrefix(string? vatNo)
    {
        if (string.IsNullOrWhiteSpace(vatNo))
        {
            return null;
        }

        var trimmed = vatNo.Trim().Replace(" ", string.Empty);
        return _prefix.Replace(trimmed, string.Empty);
    }
}
