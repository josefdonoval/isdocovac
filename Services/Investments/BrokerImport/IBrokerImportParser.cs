using Isdocovac.Models.Enums;

namespace Isdocovac.Services.Investments.BrokerImport;

public record BrokerImportParseResult(IReadOnlyList<BrokerImportRow> Rows, IReadOnlyList<string> Warnings);

public class BrokerImportRow
{
    public Guid RowId { get; init; } = Guid.NewGuid();
    public int LineNumber { get; init; }
    public string RawLine { get; init; } = string.Empty;

    public string? ExternalOrderId { get; init; }
    public string? Isin { get; init; }
    public string? ProductName { get; init; }

    public string? Symbol { get; set; }

    public DateTime TradeDateTime { get; init; }
    public decimal Quantity { get; init; }
    public decimal TradePrice { get; init; }
    public string Currency { get; init; } = "USD";

    public decimal CommissionFeeEur { get; init; }

    public bool DefaultSelected { get; init; } = true;
    public string? SkipReason { get; init; }

    public string? ParseError { get; set; }
}

public interface IBrokerImportParser
{
    Broker Broker { get; }
    Task<BrokerImportParseResult> ParseAsync(Stream fileStream, string fileName, CancellationToken ct = default);
}
