using ClosedXML.Excel;
using FluentAssertions;
using Isdocovac.Models.Enums;
using Isdocovac.Services.Investments.BrokerImport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Isdocovac.Tests.Services.Investments;

public class XtbXlsxParserTests
{
    private static XtbXlsxParser NewParser() =>
        new(NullLogger<XtbXlsxParser>.Instance);

    /// <summary>
    /// Build an in-memory XTB-shaped xlsx with given Cash Operations rows.
    /// Each row: (type, ticker, instrument, time, amount, id, comment).
    /// </summary>
    private static MemoryStream BuildWorkbook(IEnumerable<(string type, string ticker, string instrument, DateTime time, double amount, string id, string comment)> rows)
    {
        using var wb = new XLWorkbook();
        wb.AddWorksheet("Closed Positions");
        var sheet = wb.AddWorksheet("Cash Operations");

        sheet.Cell(1, 1).Value = "Account number";
        sheet.Cell(1, 2).Value = "1870893";
        sheet.Cell(2, 1).Value = "Cash Operations";
        sheet.Cell(3, 1).Value = "Date from (UTC)";
        sheet.Cell(4, 1).Value = "Date to (UTC)";

        sheet.Cell(5, 1).Value = "Type";
        sheet.Cell(5, 2).Value = "Ticker";
        sheet.Cell(5, 3).Value = "Instrument";
        sheet.Cell(5, 4).Value = "Time";
        sheet.Cell(5, 5).Value = "Amount";
        sheet.Cell(5, 6).Value = "ID";
        sheet.Cell(5, 7).Value = "Comment";
        sheet.Cell(5, 8).Value = "Product";

        var r = 6;
        foreach (var (type, ticker, instrument, time, amount, id, comment) in rows)
        {
            sheet.Cell(r, 1).Value = type;
            sheet.Cell(r, 2).Value = ticker;
            sheet.Cell(r, 3).Value = instrument;
            sheet.Cell(r, 4).Value = time;
            sheet.Cell(r, 5).Value = amount;
            sheet.Cell(r, 6).Value = id;
            sheet.Cell(r, 7).Value = comment;
            sheet.Cell(r, 8).Value = "My Trades";
            r++;
        }

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void Broker_IsXtb()
    {
        NewParser().Broker.Should().Be(Broker.Xtb);
    }

    [Fact]
    public async Task Parse_NoCurrencyPrefix_ReturnsParseError()
    {
        using var ms = BuildWorkbook(Array.Empty<(string, string, string, DateTime, double, string, string)>());
        var result = await NewParser().ParseAsync(ms, "no-prefix.xlsx");

        result.Rows.Should().HaveCount(1);
        result.Rows[0].ParseError.Should().Contain("měn");
    }

    [Fact]
    public async Task Parse_StockPurchase_BuildsBuyRow()
    {
        using var ms = BuildWorkbook(new[]
        {
            ("Stock purchase", "UBI.FR", "Ubisoft", new DateTime(2026, 5, 5, 13, 15, 13, DateTimeKind.Utc), -19.65, "1251235000", "OPEN BUY 4 @ 4.9120"),
        });
        var result = await NewParser().ParseAsync(ms, "EUR_55012937_2006-01-01_2026-05-05.xlsx");

        var row = result.Rows.Single(r => r.ParseError == null);
        row.Symbol.Should().Be("UBI");
        row.ProductName.Should().Be("Ubisoft");
        row.Quantity.Should().Be(4m);
        row.TradePrice.Should().Be(4.9120m);
        row.Currency.Should().Be("EUR");
        row.ExternalOrderId.Should().Be("1251235000");
        row.DefaultSelected.Should().BeTrue();
        row.CommissionFeeEur.Should().Be(0m);
        row.TradeDateTime.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Parse_StockSell_NegatesQuantity()
    {
        using var ms = BuildWorkbook(new[]
        {
            ("Stock sell", "T.US", "AT&T", new DateTime(2021, 10, 22, 18, 4, 2, DateTimeKind.Utc), 153.0, "189200120", "CLOSE BUY 6/11 @ 25.50"),
        });
        var result = await NewParser().ParseAsync(ms, "USD_1870893_2006-01-01_2026-05-05.xlsx");

        var row = result.Rows.Single(r => r.ParseError == null);
        row.Symbol.Should().Be("T");
        row.Quantity.Should().Be(-6m); // sells are negative; lot 6 of 11
        row.TradePrice.Should().Be(25.50m);
        row.Currency.Should().Be("USD");
        row.ExternalOrderId.Should().Be("189200120");
    }

    [Fact]
    public async Task Parse_NonTradeRow_SkippedButVisible()
    {
        using var ms = BuildWorkbook(new[]
        {
            ("Dividend", "T.US", "AT&T", new DateTime(2026, 5, 1, 9, 59, 35, DateTimeKind.Utc), 0.56, "1245306567", "T.US USD 0.2775/ SHR"),
            ("Withholding tax", "T.US", "AT&T", new DateTime(2026, 5, 1, 9, 59, 35, DateTimeKind.Utc), -0.17, "1245306568", "T.US USD WHT 30%"),
            ("Deposit", "", "", new DateTime(2025, 3, 3, 23, 52, 4, DateTimeKind.Utc), 150.0, "737881081", "PayPal deposit"),
        });
        var result = await NewParser().ParseAsync(ms, "USD_acct.xlsx");

        result.Rows.Should().HaveCount(3);
        result.Rows.Should().AllSatisfy(r =>
        {
            r.DefaultSelected.Should().BeFalse();
            r.SkipReason.Should().NotBeNull();
            r.ParseError.Should().BeNull();
            r.SkipReason.Should().Contain("není obchod");
        });
    }

    [Fact]
    public async Task Parse_StopsAtTotalRow()
    {
        // BuildWorkbook only writes data rows; manually append a Total row at the end.
        using var wb = new XLWorkbook();
        wb.AddWorksheet("Closed Positions");
        var sheet = wb.AddWorksheet("Cash Operations");
        sheet.Cell(1, 1).Value = "Account number";
        sheet.Cell(5, 1).Value = "Type";
        sheet.Cell(5, 2).Value = "Ticker";

        sheet.Cell(6, 1).Value = "Stock purchase";
        sheet.Cell(6, 2).Value = "UBI.FR";
        sheet.Cell(6, 3).Value = "Ubisoft";
        sheet.Cell(6, 4).Value = new DateTime(2026, 5, 5, 13, 15, 13, DateTimeKind.Utc);
        sheet.Cell(6, 5).Value = -19.65;
        sheet.Cell(6, 6).Value = "1251235000";
        sheet.Cell(6, 7).Value = "OPEN BUY 4 @ 4.9120";

        sheet.Cell(7, 1).Value = "Total";
        sheet.Cell(7, 5).Value = 0.74;

        // After Total there must be no more rows considered.
        sheet.Cell(8, 1).Value = "Stock purchase";
        sheet.Cell(8, 7).Value = "OPEN BUY 9 @ 9";

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        var result = await NewParser().ParseAsync(ms, "EUR_test.xlsx");

        result.Rows.Should().HaveCount(1);
        result.Rows[0].Quantity.Should().Be(4m);
    }

    [Fact]
    public async Task Parse_RealUsdExport_ProducesExpectedTradeCounts()
    {
        var path = "/Users/josef/Downloads/1870893_54396878_55012937_2006-01-01_2026-05-05/1870893/USD_1870893_2006-01-01_2026-05-05.xlsx";
        if (!File.Exists(path)) return; // skip if file not available locally

        await using var fs = File.OpenRead(path);
        var result = await NewParser().ParseAsync(fs, Path.GetFileName(path));

        var trades = result.Rows.Where(r => r.ParseError == null && r.DefaultSelected).ToList();
        trades.Count(r => r.Quantity > 0).Should().Be(31, "Stock purchase rows");
        trades.Count(r => r.Quantity < 0).Should().Be(10, "Stock sell rows");
        trades.Should().AllSatisfy(r => r.Currency.Should().Be("USD"));

        // Spot-check the AT&T close on 2021-10-22.
        var att = trades.Single(r => r.ExternalOrderId == "189200120");
        att.Symbol.Should().Be("T");
        att.Quantity.Should().Be(-6m);
        att.TradePrice.Should().Be(25.50m);
    }

    [Fact]
    public async Task Parse_RealEurExport_ProducesSinglePurchase()
    {
        var path = "/Users/josef/Downloads/1870893_54396878_55012937_2006-01-01_2026-05-05/55012937/EUR_55012937_2006-01-01_2026-05-05.xlsx";
        if (!File.Exists(path)) return; // skip if file not available locally

        await using var fs = File.OpenRead(path);
        var result = await NewParser().ParseAsync(fs, Path.GetFileName(path));

        var trades = result.Rows.Where(r => r.ParseError == null && r.DefaultSelected).ToList();
        trades.Should().HaveCount(1);
        var trade = trades.Single();
        trade.ExternalOrderId.Should().Be("1251235000");
        trade.Symbol.Should().Be("UBI");
        trade.Quantity.Should().Be(4m);
        trade.TradePrice.Should().Be(4.9120m);
        trade.Currency.Should().Be("EUR");
    }
}
