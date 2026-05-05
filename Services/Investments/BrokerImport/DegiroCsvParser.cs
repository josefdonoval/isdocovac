using System.Globalization;
using System.Text;
using Isdocovac.Models.Enums;

namespace Isdocovac.Services.Investments.BrokerImport;

/// <summary>
/// Parses Degiro Transactions.csv exports. Format:
///   Date,Time,Product,ISIN,Reference exchange,Venue,Quantity,Price,Currency,
///   Local value,Currency,Value EUR,Exchange rate,AutoFX Fee,
///   Transaction and/or third party fees EUR,Total EUR,,Order ID
/// Decimals use comma; fields containing commas are double-quoted.
/// </summary>
public class DegiroCsvParser : IBrokerImportParser
{
    private readonly ILogger<DegiroCsvParser> _logger;

    public DegiroCsvParser(ILogger<DegiroCsvParser> logger)
    {
        _logger = logger;
    }

    public Broker Broker => Broker.Degiro;

    public async Task<BrokerImportParseResult> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var rows = new List<BrokerImportRow>();
        var warnings = new List<string>();

        using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        string? line;
        var lineNumber = 0;
        var headerSkipped = false;

        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cells = SplitCsv(line);

            if (!headerSkipped && IsHeader(cells))
            {
                headerSkipped = true;
                continue;
            }

            if (cells.Count < 17)
            {
                rows.Add(new BrokerImportRow
                {
                    LineNumber = lineNumber,
                    RawLine = line,
                    ParseError = $"Řádek má {cells.Count} sloupců, očekáváno alespoň 17."
                });
                continue;
            }

            try
            {
                var row = ParseRow(cells, lineNumber, line);
                rows.Add(row);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Degiro parse failed at line {Line}", lineNumber);
                rows.Add(new BrokerImportRow
                {
                    LineNumber = lineNumber,
                    RawLine = line,
                    ParseError = $"Parse: {ex.Message}"
                });
            }
        }

        // Second pass: an Order ID whose net signed quantity is zero is a pure Auto-FX
        // bookkeeping block (Degiro books 4 rows per such block). Pre-uncheck the whole group
        // even if individual rows aren't tagged " - TD".
        var byOrderId = rows
            .Where(r => r.ParseError == null && !string.IsNullOrEmpty(r.ExternalOrderId))
            .GroupBy(r => r.ExternalOrderId!)
            .Where(g => g.Count() > 1 && g.Sum(r => r.Quantity) == 0m)
            .Select(g => g.Key)
            .ToHashSet();

        if (byOrderId.Count > 0)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.ExternalOrderId != null && byOrderId.Contains(r.ExternalOrderId))
                {
                    rows[i] = new BrokerImportRow
                    {
                        RowId = r.RowId,
                        LineNumber = r.LineNumber,
                        RawLine = r.RawLine,
                        ExternalOrderId = r.ExternalOrderId,
                        Isin = r.Isin,
                        ProductName = r.ProductName,
                        Symbol = r.Symbol,
                        TradeDateTime = r.TradeDateTime,
                        Quantity = r.Quantity,
                        TradePrice = r.TradePrice,
                        Currency = r.Currency,
                        CommissionFeeEur = r.CommissionFeeEur,
                        DefaultSelected = false,
                        SkipReason = "Auto-FX blok (Order ID s nulovou bilancí)",
                        ParseError = r.ParseError,
                    };
                }
            }
        }

        return new BrokerImportParseResult(rows, warnings);
    }

    private static BrokerImportRow ParseRow(IReadOnlyList<string> cells, int lineNumber, string raw)
    {
        var dateStr = cells[0].Trim();
        var timeStr = cells[1].Trim();
        if (string.IsNullOrEmpty(timeStr)) timeStr = "00:00";

        var dt = DateTime.ParseExact(
            $"{dateStr} {timeStr}",
            new[] { "dd-MM-yyyy HH:mm", "dd-MM-yyyy H:mm", "dd-MM-yyyy" },
            CultureInfo.InvariantCulture,
            DateTimeStyles.None);

        var product = cells[2].Trim();
        var isin = string.IsNullOrWhiteSpace(cells[3]) ? null : cells[3].Trim().ToUpperInvariant();
        var qty = ParseDec(cells[6]);
        var price = ParseDec(cells[7]);
        var currency = string.IsNullOrWhiteSpace(cells[8]) ? "EUR" : cells[8].Trim().ToUpperInvariant();
        var autoFxFee = ParseDecNullable(cells[13]) ?? 0m;
        var transactionFee = ParseDecNullable(cells[14]) ?? 0m;
        var feeEur = Math.Abs(autoFxFee) + Math.Abs(transactionFee);

        // Order ID is the last non-empty trailing cell. The CSV has a stray empty
        // column before the Order ID, so look at the tail.
        string? orderId = null;
        for (int i = cells.Count - 1; i >= 16; i--)
        {
            var v = cells[i].Trim();
            if (!string.IsNullOrEmpty(v))
            {
                orderId = v;
                break;
            }
        }
        // "-1" is Degiro's sentinel for migrated/historical entries — not a real Order ID.
        if (orderId == "-1") orderId = null;

        var (defaultSelected, skipReason) = ClassifyRow(product, qty, price, orderId);

        return new BrokerImportRow
        {
            LineNumber = lineNumber,
            RawLine = raw,
            ExternalOrderId = orderId,
            Isin = isin,
            ProductName = product,
            TradeDateTime = dt,
            Quantity = qty,
            TradePrice = price,
            Currency = currency,
            CommissionFeeEur = feeEur,
            DefaultSelected = defaultSelected,
            SkipReason = skipReason,
        };
    }

    private static (bool selected, string? reason) ClassifyRow(string product, decimal qty, decimal price, string? orderId)
    {
        if (product.EndsWith(" - TD", StringComparison.Ordinal))
            return (false, "Auto-FX záznam (TD)");
        if (qty == 0m)
            return (false, "Quantity = 0 (corp action / informativní)");
        if (price == 0m)
            return (false, "Price = 0");
        if (string.IsNullOrEmpty(orderId))
            return (false, "Bez Order ID (nelze deduplikovat)");
        return (true, null);
    }

    private static bool IsHeader(IReadOnlyList<string> cells)
    {
        if (cells.Count < 4) return false;
        return cells[0].Trim().Equals("Date", StringComparison.OrdinalIgnoreCase)
            && cells[1].Trim().Equals("Time", StringComparison.OrdinalIgnoreCase)
            && cells[2].Trim().Equals("Product", StringComparison.OrdinalIgnoreCase);
    }

    private static decimal ParseDec(string s)
    {
        var trimmed = s.Trim().Replace(",", ".");
        if (string.IsNullOrEmpty(trimmed)) return 0m;
        return decimal.Parse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture);
    }

    private static decimal? ParseDecNullable(string s)
    {
        var trimmed = s.Trim().Replace(",", ".");
        if (string.IsNullOrEmpty(trimmed)) return null;
        return decimal.Parse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Splits a single CSV line. Handles double-quoted fields and escaped quotes (""). Always
    /// returns at least one element.
    /// </summary>
    public static List<string> SplitCsv(string line)
    {
        var cells = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    cells.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
        }
        cells.Add(sb.ToString());
        return cells;
    }
}
