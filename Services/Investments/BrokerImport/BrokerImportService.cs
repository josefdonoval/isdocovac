using System.Security.Cryptography;
using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Isdocovac.Providers;
using Isdocovac.Providers.Investments;
using Isdocovac.Services.Fx;
using BrokerImportEntity = Isdocovac.Models.BrokerImport;

namespace Isdocovac.Services.Investments.BrokerImport;

public class BrokerImportPreviewRow
{
    public Guid RowId { get; init; }
    public int LineNumber { get; init; }
    public string? ExternalOrderId { get; init; }
    public string? Isin { get; init; }
    public string? ProductName { get; init; }
    public string Symbol { get; set; } = string.Empty;
    public DateTime TradeDateTime { get; init; }
    public decimal SignedQuantity { get; init; }
    public decimal TradePrice { get; init; }
    public string Currency { get; init; } = "USD";
    public decimal CommissionFeeEur { get; init; }
    public decimal CommissionFeeTradeCurrency { get; set; }
    public bool DefaultSelected { get; set; }
    public string? SkipReason { get; set; }
    public bool DuplicateLocked { get; set; }
    public string? Status { get; set; }
    public Share? Trade { get; set; }
    public string? Error { get; set; }
}

public record BrokerImportPreview(
    BrokerImportEntity Import,
    IReadOnlyList<BrokerImportPreviewRow> Rows,
    IReadOnlyList<string> Warnings);

public interface IBrokerImportService
{
    Task<(BrokerImportEntity Import, BrokerImportEntity? Existing)> UploadAsync(
        Guid companyId, Broker broker, string fileName, long fileSize, string contentType, Stream fileStream, CancellationToken ct = default);

    Task<BrokerImportPreview> ParseAsync(Guid brokerImportId, CancellationToken ct = default);

    Task<int> ImportAsync(Guid brokerImportId, IReadOnlyList<BrokerImportPreviewRow> rows, CancellationToken ct = default);
}

public class BrokerImportService : IBrokerImportService
{
    private readonly IBrokerImportProvider _imports;
    private readonly IShareProvider _shareProvider;
    private readonly IShareCalculationService _shareCalc;
    private readonly ICnbExchangeRateService _cnb;
    private readonly IAzureBlobStorageProvider _blob;
    private readonly IConfiguration _configuration;
    private readonly IEnumerable<IBrokerImportParser> _parsers;
    private readonly ISymbolResolutionService _symbols;
    private readonly ILogger<BrokerImportService> _logger;

    public BrokerImportService(
        IBrokerImportProvider imports,
        IShareProvider shareProvider,
        IShareCalculationService shareCalc,
        ICnbExchangeRateService cnb,
        IAzureBlobStorageProvider blob,
        IConfiguration configuration,
        IEnumerable<IBrokerImportParser> parsers,
        ISymbolResolutionService symbols,
        ILogger<BrokerImportService> logger)
    {
        _imports = imports;
        _shareProvider = shareProvider;
        _shareCalc = shareCalc;
        _cnb = cnb;
        _blob = blob;
        _configuration = configuration;
        _parsers = parsers;
        _symbols = symbols;
        _logger = logger;
    }

    private string ContainerName =>
        _configuration["AzureStorage:BrokerImportContainerName"] ?? "broker-imports";

    public async Task<(BrokerImportEntity Import, BrokerImportEntity? Existing)> UploadAsync(
        Guid companyId, Broker broker, string fileName, long fileSize, string contentType, Stream fileStream, CancellationToken ct = default)
    {
        // Buffer the stream so we can hash + upload + reset for parse later.
        using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms, ct);
        ms.Position = 0;

        var hash = ComputeSha256(ms);
        ms.Position = 0;

        var existing = await _imports.FindByHashAsync(companyId, hash);
        if (existing != null)
        {
            return (existing, existing);
        }

        var importId = Guid.NewGuid();
        var safeName = SanitizeFileName(fileName);
        var blobName = $"{companyId}/{importId}/{safeName}";
        var blobUrl = await _blob.UploadBlobAsync(ContainerName, blobName, ms, contentType);

        var entity = new BrokerImportEntity
        {
            Id = importId,
            CompanyId = companyId,
            Broker = broker,
            FileName = fileName,
            FileSize = fileSize,
            ContentType = contentType,
            BlobContainerName = ContainerName,
            BlobName = blobName,
            BlobUrl = blobUrl,
            FileHash = hash,
            Status = BrokerImportStatus.Pending,
            UploadedAt = DateTime.UtcNow,
        };

        await _imports.CreateAsync(entity);
        return (entity, null);
    }

    public async Task<BrokerImportPreview> ParseAsync(Guid brokerImportId, CancellationToken ct = default)
    {
        var import = await _imports.GetAsync(brokerImportId)
            ?? throw new InvalidOperationException($"BrokerImport {brokerImportId} not found.");

        var parser = _parsers.FirstOrDefault(p => p.Broker == import.Broker)
            ?? throw new InvalidOperationException($"No parser registered for broker {import.Broker}.");

        await using var stream = await _blob.DownloadBlobAsync(import.BlobContainerName, import.BlobName);
        BrokerImportParseResult parsed;
        try
        {
            parsed = await parser.ParseAsync(stream, import.FileName, ct);
        }
        catch (Exception ex)
        {
            import.Status = BrokerImportStatus.Failed;
            import.ErrorMessage = ex.Message;
            await _imports.UpdateAsync(import);
            throw;
        }

        // Resolve symbols once for the whole batch.
        var isins = parsed.Rows.Where(r => !string.IsNullOrWhiteSpace(r.Isin)).Select(r => r.Isin!).ToList();
        var symbolMap = await _symbols.ResolveAsync(import.CompanyId, isins, ct);

        // Row-level dedup against existing shares.
        var orderIds = parsed.Rows
            .Where(r => !string.IsNullOrWhiteSpace(r.ExternalOrderId))
            .Select(r => r.ExternalOrderId!)
            .ToList();
        var existingOrderIds = await _shareProvider.GetExistingExternalOrderIdsAsync(
            import.CompanyId, import.Broker, orderIds);

        var rows = new List<BrokerImportPreviewRow>(parsed.Rows.Count);
        foreach (var src in parsed.Rows)
        {
            var symbol = src.Symbol;
            if (string.IsNullOrWhiteSpace(symbol) && !string.IsNullOrWhiteSpace(src.Isin))
            {
                symbolMap.TryGetValue(src.Isin!, out symbol);
            }
            symbol ??= src.Isin ?? string.Empty;

            decimal feeTrade = 0m;
            string? prepError = src.ParseError;
            Share? trade = null;

            var preview = new BrokerImportPreviewRow
            {
                RowId = src.RowId,
                LineNumber = src.LineNumber,
                ExternalOrderId = src.ExternalOrderId,
                Isin = src.Isin,
                ProductName = src.ProductName,
                Symbol = symbol,
                TradeDateTime = src.TradeDateTime,
                SignedQuantity = src.Quantity,
                TradePrice = src.TradePrice,
                Currency = src.Currency,
                CommissionFeeEur = src.CommissionFeeEur,
                DefaultSelected = src.DefaultSelected,
                SkipReason = src.SkipReason,
            };

            if (!string.IsNullOrEmpty(src.ExternalOrderId)
                && existingOrderIds.Contains(src.ExternalOrderId))
            {
                preview.DuplicateLocked = true;
                preview.DefaultSelected = false;
                preview.SkipReason = "Už importováno (Order ID v DB)";
            }

            if (prepError == null && src.Quantity != 0m && src.TradePrice != 0m)
            {
                try
                {
                    feeTrade = await ConvertEurFeeToTradeCurrencyAsync(
                        src.CommissionFeeEur, src.Currency, DateOnly.FromDateTime(src.TradeDateTime), ct);

                    var code = src.Quantity >= 0 ? ShareTradeCode.Buy : ShareTradeCode.Sell;
                    var signedQty = code == ShareTradeCode.Sell ? -Math.Abs(src.Quantity) : Math.Abs(src.Quantity);

                    trade = new Share
                    {
                        CompanyId = import.CompanyId,
                        Symbol = (symbol ?? string.Empty).Trim(),
                        Isin = src.Isin,
                        Name = src.ProductName,
                        TradeDateTime = src.TradeDateTime,
                        Quantity = signedQty,
                        TradePrice = src.TradePrice,
                        Currency = src.Currency,
                        Broker = import.Broker,
                        CommissionFee = feeTrade,
                        Code = code,
                        ExternalOrderId = src.ExternalOrderId,
                        BrokerImportId = import.Id,
                    };

                    await _shareCalc.PrepareAsync(trade, ct);
                }
                catch (Exception ex)
                {
                    prepError = $"FX/calc: {ex.Message}";
                    trade = null;
                }
            }

            preview.CommissionFeeTradeCurrency = feeTrade;
            preview.Trade = trade;
            preview.Error = prepError;
            preview.Status = prepError != null
                ? prepError
                : preview.DuplicateLocked
                    ? "Duplicate"
                    : preview.SkipReason ?? "OK";

            rows.Add(preview);
        }

        // Persist preview-time stats. RowsImported updated at ImportAsync.
        import.RowsTotal = rows.Count;
        import.RowsSkipped = rows.Count(r => !r.DefaultSelected && !r.DuplicateLocked);
        import.RowsDuplicate = rows.Count(r => r.DuplicateLocked);
        import.Status = BrokerImportStatus.Parsed;
        import.ParsedAt = DateTime.UtcNow;
        import.ErrorMessage = null;
        await _imports.UpdateAsync(import);

        return new BrokerImportPreview(import, rows, parsed.Warnings);
    }

    public async Task<int> ImportAsync(Guid brokerImportId, IReadOnlyList<BrokerImportPreviewRow> rows, CancellationToken ct = default)
    {
        var import = await _imports.GetAsync(brokerImportId)
            ?? throw new InvalidOperationException($"BrokerImport {brokerImportId} not found.");

        var trades = rows
            .Where(r => r.DefaultSelected && r.Trade != null && r.Error == null && !r.DuplicateLocked)
            .Select(r =>
            {
                var t = r.Trade!;
                t.Symbol = (r.Symbol ?? t.Symbol).Trim();
                t.TradeDateTime = ToUtc(t.TradeDateTime);
                return t;
            })
            .ToList();

        if (trades.Count == 0)
        {
            return 0;
        }

        await _shareProvider.CreateBulkAsync(trades);

        foreach (var symbol in trades.Select(t => t.Symbol).Distinct())
        {
            try
            {
                await _shareCalc.RecomputeMatchingAsync(import.CompanyId, symbol, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FIFO recompute failed for {Symbol} after import {ImportId}", symbol, brokerImportId);
            }
        }

        import.Status = BrokerImportStatus.Imported;
        import.ImportedAt = DateTime.UtcNow;
        import.RowsImported = trades.Count;
        await _imports.UpdateAsync(import);

        return trades.Count;
    }

    private async Task<decimal> ConvertEurFeeToTradeCurrencyAsync(decimal feeEur, string tradeCurrency, DateOnly date, CancellationToken ct)
    {
        if (feeEur == 0m) return 0m;
        if (string.Equals(tradeCurrency, "EUR", StringComparison.OrdinalIgnoreCase)) return feeEur;

        // Bridge through CZK using CNB rates. CZK itself has no rate (it's CNB's base) — for
        // CZK-denominated trades we stop at the CZK step.
        var eurRate = await _cnb.GetRateAsync("EUR", date, ct);
        if (eurRate.Amount <= 0) return 0m;
        var czk = feeEur * eurRate.Rate / eurRate.Amount;

        if (string.Equals(tradeCurrency, "CZK", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Round(czk, 2, MidpointRounding.AwayFromZero);
        }

        var tradeRate = await _cnb.GetRateAsync(tradeCurrency, date, ct);
        if (tradeRate.Amount <= 0 || tradeRate.Rate <= 0) return 0m;
        var trade = czk * tradeRate.Amount / tradeRate.Rate;
        return Math.Round(trade, 2, MidpointRounding.AwayFromZero);
    }

    private static string ComputeSha256(Stream stream)
    {
        using var sha = SHA256.Create();
        stream.Position = 0;
        var bytes = sha.ComputeHash(stream);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(clean) ? "upload.csv" : clean;
    }

    private static DateTime ToUtc(DateTime dt) => dt.Kind switch
    {
        DateTimeKind.Utc => dt,
        DateTimeKind.Local => dt.ToUniversalTime(),
        _ => DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime(),
    };
}
