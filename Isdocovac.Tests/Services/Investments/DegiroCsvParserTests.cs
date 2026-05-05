using System.Text;
using FluentAssertions;
using Isdocovac.Models.Enums;
using Isdocovac.Services.Investments.BrokerImport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Isdocovac.Tests.Services.Investments;

public class DegiroCsvParserTests
{
    private const string Header =
        "Date,Time,Product,ISIN,Reference exchange,Venue,Quantity,Price,,Local value,,Value EUR,Exchange rate,AutoFX Fee,Transaction and/or third party fees EUR,Total EUR,Order ID,";

    private static async Task<BrokerImportParseResult> ParseAsync(params string[] lines)
    {
        var csv = string.Join("\n", lines);
        var bytes = Encoding.UTF8.GetBytes(csv);
        await using var ms = new MemoryStream(bytes);
        var parser = new DegiroCsvParser(NullLogger<DegiroCsvParser>.Instance);
        return await parser.ParseAsync(ms, "Transactions.csv");
    }

    [Fact]
    public void Broker_IsDegiro()
    {
        new DegiroCsvParser(NullLogger<DegiroCsvParser>.Instance).Broker.Should().Be(Broker.Degiro);
    }

    [Fact]
    public void SplitCsv_HandlesQuotedDecimalCommas()
    {
        var cells = DegiroCsvParser.SplitCsv("a,\"27,9900\",c");
        cells.Should().Equal("a", "27,9900", "c");
    }

    [Fact]
    public void SplitCsv_HandlesEmptyCells()
    {
        var cells = DegiroCsvParser.SplitCsv("a,,b,");
        cells.Should().Equal("a", "", "b", "");
    }

    [Fact]
    public async Task Parse_SkipsHeaderRow()
    {
        var result = await ParseAsync(Header,
            "06-08-2025,17:54,MARQETA INC CLASS A,US57142B1044,NDQ,ARCX,-57,\"5,5800\",USD,\"318,06\",USD,\"273,18\",\"1,1643\",\"-0,68\",\"-2,00\",\"270,50\",,f8a98dff-5f5c-454c-8b94-acb7e498479f");
        result.Rows.Should().HaveCount(1);
        result.Rows[0].ProductName.Should().Be("MARQETA INC CLASS A");
    }

    [Fact]
    public async Task Parse_DegiroRow_ExtractsAllFields()
    {
        var result = await ParseAsync(Header,
            "06-08-2025,17:54,MARQETA INC CLASS A,US57142B1044,NDQ,ARCX,-57,\"5,5800\",USD,\"318,06\",USD,\"273,18\",\"1,1643\",\"-0,68\",\"-2,00\",\"270,50\",,f8a98dff-5f5c-454c-8b94-acb7e498479f");

        var row = result.Rows.Single();
        row.ProductName.Should().Be("MARQETA INC CLASS A");
        row.Isin.Should().Be("US57142B1044");
        row.Quantity.Should().Be(-57m);
        row.TradePrice.Should().Be(5.58m);
        row.Currency.Should().Be("USD");
        row.CommissionFeeEur.Should().Be(2.68m); // |-0.68| + |-2.00|
        row.ExternalOrderId.Should().Be("f8a98dff-5f5c-454c-8b94-acb7e498479f");
        row.TradeDateTime.Should().Be(new DateTime(2025, 8, 6, 17, 54, 0));
        row.DefaultSelected.Should().BeTrue();
        row.SkipReason.Should().BeNull();
    }

    [Fact]
    public async Task Parse_TdProduct_DefaultsToUnchecked()
    {
        var result = await ParseAsync(Header,
            "17-02-2026,09:48,WARNER BROS DISCOVERY INC - TD,US9344231041,NDQ,,-7,\"27,9900\",USD,\"195,93\",USD,\"165,27\",\"1,1855\",\"0,00\",,\"165,27\",,3a75a472-58eb-44a1-8227-c2f3d5472166");

        var row = result.Rows.Single();
        row.DefaultSelected.Should().BeFalse();
        row.SkipReason.Should().Contain("TD");
    }

    [Fact]
    public async Task Parse_ZeroQuantity_DefaultsToUnchecked()
    {
        var result = await ParseAsync(Header,
            "01-04-2024,00:00,SOLVENTUM CORP,US83444M1018,NSY,,0,\"0,0000\",USD,\"0,00\",USD,\"0,00\",\"1,0866\",\"0,00\",,\"0,00\",,");

        var row = result.Rows.Single();
        row.DefaultSelected.Should().BeFalse();
        row.SkipReason.Should().Contain("Quantity = 0");
    }

    [Fact]
    public async Task Parse_MinusOneOrderId_TreatedAsNull()
    {
        var result = await ParseAsync(Header,
            "02-02-2022,15:40,ACTIVISION BLIZZARD I,US00507V1098,NDQ,,15,\"79,0600\",USD,\"-1185,90\",USD,\"-1049,05\",\"1,1305\",\"0,00\",,\"-1049,05\",,-1");

        var row = result.Rows.Single();
        row.ExternalOrderId.Should().BeNull();
        row.DefaultSelected.Should().BeFalse();
        row.SkipReason.Should().Contain("Order ID");
    }

    [Fact]
    public async Task Parse_PreservesSignedQuantityForBuyAndSell()
    {
        var result = await ParseAsync(Header,
            // Buy (positive qty)
            "13-05-2022,16:03,AT&T INC,US00206R1023,NSY,CDED,10,\"19,6850\",USD,\"-196,85\",USD,\"-189,83\",\"1,0370\",\"-0,47\",\"-0,50\",\"-190,80\",,03ad0c11-6a86-45c9-9fed-f3cba4964af9",
            // Sell (negative qty)
            "06-08-2025,17:48,ROKU INC CLASS A,US77543R1023,NDQ,CDED,-1,\"85,1000\",USD,\"85,10\",USD,\"73,14\",\"1,1635\",\"-0,18\",\"-2,00\",\"70,96\",,2ef3872b-5d28-4bf0-ad4a-2aec6b7a641c");

        result.Rows.Should().HaveCount(2);
        result.Rows[0].Quantity.Should().Be(10m);
        result.Rows[1].Quantity.Should().Be(-1m);
    }

    [Fact]
    public async Task Parse_NonEurCurrency_IsCarriedThrough()
    {
        var result = await ParseAsync(Header,
            "09-05-2023,12:01,COLT CZ GROUP SE,CZ0009008942,PSE,XPRA,12,\"598,0000\",CZK,\"-7176,00\",CZK,\"-307,02\",\"23,3734\",\"0,00\",\"-1,71\",\"-308,73\",,9f9b1832-64e6-4f7a-9db8-7a9895a2ca91");

        var row = result.Rows.Single();
        row.Currency.Should().Be("CZK");
        row.TradePrice.Should().Be(598m);
        row.CommissionFeeEur.Should().Be(1.71m);
    }

    [Fact]
    public async Task Parse_MalformedRow_RecordsParseError()
    {
        var result = await ParseAsync(Header,
            "this,is,not,enough,columns");

        result.Rows.Should().HaveCount(1);
        result.Rows[0].ParseError.Should().NotBeNull();
    }

    [Fact]
    public async Task Parse_NetZeroOrderIdGroup_AllRowsDefaultUnchecked()
    {
        // Real Degiro Auto-FX block: 4 rows for one Order ID, net qty = 0. After the second pass,
        // even rows without " - TD" suffix should be pre-unchecked because the group nets to zero.
        var orderId = "3a75a472-58eb-44a1-8227-c2f3d5472166";
        var result = await ParseAsync(Header,
            $"17-02-2026,09:48,WARNER BROS DISCOVERY INC,US9344231041,NDQ,,7,\"27,9900\",USD,\"-195,93\",USD,\"-165,27\",\"1,1855\",\"0,00\",,\"-165,27\",,{orderId}",
            $"17-02-2026,09:48,WARNER BROS DISCOVERY INC - TD,US9344231041,NDQ,,-7,\"27,9900\",USD,\"195,93\",USD,\"165,27\",\"1,1855\",\"0,00\",,\"165,27\",,{orderId}",
            $"17-02-2026,09:48,WARNER BROS DISCOVERY INC - TD,US9344231041,NDQ,,7,\"27,9900\",USD,\"-195,93\",USD,\"-165,44\",\"1,1843\",\"0,00\",,\"-165,44\",,{orderId}",
            $"17-02-2026,09:48,WARNER BROS DISCOVERY INC,US9344231041,NDQ,,-7,\"27,9900\",USD,\"195,93\",USD,\"165,44\",\"1,1843\",\"0,00\",,\"165,44\",,{orderId}");

        result.Rows.Should().HaveCount(4);
        result.Rows.Should().AllSatisfy(r =>
        {
            r.DefaultSelected.Should().BeFalse();
            r.SkipReason.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task Parse_MultiFillSameOrderId_StaysSelected()
    {
        // Multi-fill: real Degiro split execution — same Order ID, both rows positive qty, net != 0.
        var orderId = "a6359331-6363-41d9-878c-cf9eb419aab8";
        var result = await ParseAsync(Header,
            $"13-10-2021,11:33,CD PROJEKT SA,PLOPTTC00011,WSE,XWAR,4,\"206,6000\",PLN,\"-826,40\",PLN,\"-180,66\",\"4,5744\",\"-0,18\",\"-0,29\",\"-181,13\",,{orderId}",
            $"13-10-2021,11:33,CD PROJEKT SA,PLOPTTC00011,WSE,XWAR,2,\"206,6000\",PLN,\"-413,20\",PLN,\"-90,33\",\"4,5744\",\"-0,09\",\"-5,14\",\"-95,56\",,{orderId}");

        result.Rows.Should().HaveCount(2);
        result.Rows.Should().AllSatisfy(r =>
        {
            r.DefaultSelected.Should().BeTrue();
            r.SkipReason.Should().BeNull();
        });
    }

    [Fact]
    public async Task Parse_BlankLinesAreSkipped()
    {
        var result = await ParseAsync(Header,
            "",
            "06-08-2025,17:54,MARQETA INC CLASS A,US57142B1044,NDQ,ARCX,-57,\"5,5800\",USD,\"318,06\",USD,\"273,18\",\"1,1643\",\"-0,68\",\"-2,00\",\"270,50\",,f8a98dff-5f5c-454c-8b94-acb7e498479f",
            "");

        result.Rows.Should().HaveCount(1);
    }
}
