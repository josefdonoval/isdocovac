using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Isdocovac.Models.Enums;

namespace Isdocovac.Services.Investments.BrokerImport;

/// <summary>
/// Parses XTB xlsx exports. Two sheets: "Closed Positions" (ignored — only matched trades)
/// and "Cash Operations" (every cash event including individual buys/sells).
///
/// Cash Operations columns (header on row 5, data from row 6):
///   Type | Ticker | Instrument | Time | Amount | ID | Comment | Product
///
/// Trade rows have Type ∈ {"Stock purchase", "Stock sell"}; quantity and unit price are
/// embedded in Comment (e.g. "OPEN BUY 4 @ 4.9120", "CLOSE BUY 6/11 @ 25.50").
/// XTB exports are per-currency-account; currency is derived from the filename prefix.
/// </summary>
public class XtbXlsxParser : IBrokerImportParser
{
    private readonly ILogger<XtbXlsxParser> _logger;

    public XtbXlsxParser(ILogger<XtbXlsxParser> logger)
    {
        _logger = logger;
    }

    public Broker Broker => Broker.Xtb;

    private static readonly HashSet<string> KnownCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "USD", "EUR", "GBP", "CHF", "PLN", "CZK", "HUF",
    };

    private static readonly Regex CommentRegex = new(
        @"^(?<action>OPEN|CLOSE)\s+(?<side>BUY|SELL)\s+(?<qty>\d+(?:\.\d+)?)(?:/\d+(?:\.\d+)?)?\s+@\s+(?<price>\d+(?:\.\d+)?)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CurrencyFromFileNameRegex = new(
        @"^(?<ccy>[A-Z]{3})_",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task<BrokerImportParseResult> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var rows = new List<BrokerImportRow>();
        var warnings = new List<string>();

        var currency = DetectCurrencyFromFileName(fileName);
        if (currency == null)
        {
            rows.Add(new BrokerImportRow
            {
                LineNumber = 0,
                RawLine = fileName,
                ParseError = "Nelze rozpoznat měnu z názvu souboru — XTB očekává prefix USD_/EUR_/GBP_/… (např. USD_1234567_…xlsx).",
            });
            return Task.FromResult(new BrokerImportParseResult(rows, warnings));
        }

        // ClosedXML needs a seekable stream. The blob download stream usually is, but copy
        // defensively to a MemoryStream — xlsx files are small (the test files are <100KB).
        using var ms = new MemoryStream();
        fileStream.CopyTo(ms);
        ms.Position = 0;

        using var workbook = new XLWorkbook(ms);

        var sheet = workbook.Worksheets.FirstOrDefault(w =>
            string.Equals(w.Name, "Cash Operations", StringComparison.OrdinalIgnoreCase));
        if (sheet == null)
        {
            warnings.Add("Sešit \"Cash Operations\" nebyl nalezen — zkontroluj, že jde o XTB export.");
            return Task.FromResult(new BrokerImportParseResult(rows, warnings));
        }

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        for (int r = 6; r <= lastRow; r++)
        {
            ct.ThrowIfCancellationRequested();
            var typeCell = sheet.Cell(r, 1).GetString().Trim();
            if (string.IsNullOrEmpty(typeCell)) continue;
            if (typeCell.Equals("Total", StringComparison.OrdinalIgnoreCase)) break;

            var ticker = sheet.Cell(r, 2).GetString().Trim();
            var instrument = sheet.Cell(r, 3).GetString().Trim();
            var timeCell = sheet.Cell(r, 4);
            var amount = sheet.Cell(r, 5).GetDouble();
            var id = sheet.Cell(r, 6).GetString().Trim();
            var comment = sheet.Cell(r, 7).GetString().Trim();

            DateTime time;
            try
            {
                time = DateTime.SpecifyKind(timeCell.GetDateTime(), DateTimeKind.Utc);
            }
            catch (Exception ex)
            {
                rows.Add(new BrokerImportRow
                {
                    LineNumber = r,
                    RawLine = $"{typeCell} | {ticker} | {comment}",
                    ParseError = $"Neplatné datum: {ex.Message}",
                });
                continue;
            }

            var rawLine = $"{typeCell} | {ticker} | {instrument} | {time:yyyy-MM-dd HH:mm:ss} | {amount} | {id} | {comment}";

            var isTrade = typeCell.Equals("Stock purchase", StringComparison.OrdinalIgnoreCase)
                          || typeCell.Equals("Stock sell", StringComparison.OrdinalIgnoreCase);

            if (!isTrade)
            {
                rows.Add(new BrokerImportRow
                {
                    LineNumber = r,
                    RawLine = rawLine,
                    ExternalOrderId = string.IsNullOrEmpty(id) ? null : id,
                    Isin = null,
                    ProductName = string.IsNullOrEmpty(instrument) ? null : instrument,
                    Symbol = StripExchangeSuffix(ticker),
                    TradeDateTime = time,
                    Quantity = 0m,
                    TradePrice = 0m,
                    Currency = currency,
                    CommissionFeeEur = 0m,
                    DefaultSelected = false,
                    SkipReason = $"{typeCell} (není obchod)",
                });
                continue;
            }

            var match = CommentRegex.Match(comment);
            if (!match.Success)
            {
                rows.Add(new BrokerImportRow
                {
                    LineNumber = r,
                    RawLine = rawLine,
                    ExternalOrderId = string.IsNullOrEmpty(id) ? null : id,
                    Isin = null,
                    ProductName = string.IsNullOrEmpty(instrument) ? null : instrument,
                    Symbol = StripExchangeSuffix(ticker),
                    TradeDateTime = time,
                    Quantity = 0m,
                    TradePrice = 0m,
                    Currency = currency,
                    CommissionFeeEur = 0m,
                    ParseError = $"Komentář neodpovídá očekávanému formátu \"OPEN/CLOSE BUY/SELL qty @ price\": {comment}",
                });
                continue;
            }

            var qty = decimal.Parse(match.Groups["qty"].Value, NumberStyles.Any, CultureInfo.InvariantCulture);
            var price = decimal.Parse(match.Groups["price"].Value, NumberStyles.Any, CultureInfo.InvariantCulture);
            var signedQty = typeCell.Equals("Stock sell", StringComparison.OrdinalIgnoreCase)
                ? -Math.Abs(qty)
                : Math.Abs(qty);

            // Sanity check: |Amount| should be close to qty * price (XTB stocks are commission-free).
            string? skipReason = null;
            var defaultSelected = true;
            var expected = (double)(qty * price);
            var actual = Math.Abs(amount);
            if (expected > 0 && Math.Abs(actual - expected) / expected > 0.05)
            {
                warnings.Add($"Řádek {r}: |Amount|={actual:0.##} se výrazně liší od qty×price={expected:0.##} ({comment}).");
            }

            rows.Add(new BrokerImportRow
            {
                LineNumber = r,
                RawLine = rawLine,
                ExternalOrderId = string.IsNullOrEmpty(id) ? null : id,
                Isin = null,
                ProductName = string.IsNullOrEmpty(instrument) ? null : instrument,
                Symbol = StripExchangeSuffix(ticker),
                TradeDateTime = time,
                Quantity = signedQty,
                TradePrice = price,
                Currency = currency,
                CommissionFeeEur = 0m,
                DefaultSelected = defaultSelected,
                SkipReason = skipReason,
            });
        }

        return Task.FromResult(new BrokerImportParseResult(rows, warnings));
    }

    private static string? DetectCurrencyFromFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        var match = CurrencyFromFileNameRegex.Match(name);
        if (!match.Success) return null;
        var ccy = match.Groups["ccy"].Value.ToUpperInvariant();
        return KnownCurrencies.Contains(ccy) ? ccy : null;
    }

    private static string StripExchangeSuffix(string ticker)
    {
        if (string.IsNullOrEmpty(ticker)) return string.Empty;
        var dot = ticker.IndexOf('.');
        return dot > 0 ? ticker.Substring(0, dot) : ticker;
    }
}
