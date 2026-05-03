using FluentAssertions;
using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Isdocovac.Services.Fx;
using Isdocovac.Services.Vat;
using Moq;

namespace Isdocovac.Tests.Services.Vat;

public class VatCalculationServiceTests
{
    private static Company OwnCompany() => new()
    {
        Id = Guid.NewGuid(),
        OwnerUserId = Guid.NewGuid(),
        Name = "Josef Donoval",
        IsLegalEntity = false,
        Dic = "8605256220",
        FirstName = "Josef",
        LastName = "Donoval",
        Street = "Stašov",
        HouseNumber = "52",
        City = "Stašov",
        Zip = "26751",
        CountryCode = "CZ",
        Email = "josef.donoval@hotmail.com",
        TaxOfficeCode = 452,
        TaxOfficeBranchCode = 2104,
    };

    private static Mock<ICnbExchangeRateService> PassThroughFx()
    {
        var m = new Mock<ICnbExchangeRateService>();
        m.Setup(s => s.ConvertToCzkAsync(It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
         .ReturnsAsync((decimal amt, string _, DateOnly _, CancellationToken _) => amt);
        return m;
    }

    [Fact]
    public async Task Build_OutboundDomestic_ClassifiesByTotalAndAggregatesRates()
    {
        var userId = Guid.NewGuid();
        var invoices = new List<Invoice>
        {
            MakeOutbound(userId, "2026020001", new DateTime(2026, 2, 2), 21776.87m, 4573.13m, clientDic: ""),
            MakeOutbound(userId, "2026020002", new DateTime(2026, 2, 28), 73500m, 15435m, clientDic: "06990258"),
        };

        var svc = new VatCalculationService(PassThroughFx().Object);
        var report = await svc.BuildAsync(invoices, OwnCompany(), 2026, 2);

        report.A4Rows.Should().HaveCount(2);
        report.A5Aggregate.HasAnyValue.Should().BeFalse();
        report.Obrat23.Should().Be(95276.87m);
        report.Dan23.Should().Be(20008.13m);
        report.KhObrat23.Should().Be(95276.87m);
    }

    [Fact]
    public async Task Build_InboundForeign_CreatesA2RowsAndReverseCharge()
    {
        var userId = Guid.NewGuid();
        var invoices = new List<Invoice>
        {
            // EU supplier (DE)
            MakeInbound(userId, "081000710149", new DateTime(2026, 2, 11), 176.5m, 0m,
                supplierCountry: "DE", supplierVatNo: "DE812871812"),
            // non-EU country (routed to "služby z 3. zemí")
            MakeInbound(userId, "FIA1IIHO-0002", new DateTime(2026, 2, 9), 435.96m, 0m,
                supplierCountry: "US", supplierVatNo: ""),
        };

        var svc = new VatCalculationService(PassThroughFx().Object);
        var report = await svc.BuildAsync(invoices, OwnCompany(), 2026, 2);

        report.A2Rows.Should().HaveCount(2);
        // EU entry bubbles to p_sl23_e
        report.PSl23Eu.Should().Be(176.5m);
        report.DanPSl23Eu.Should().Be(Math.Round(176.5m * 0.21m, 2, MidpointRounding.AwayFromZero));
        // non-EU entry bubbles to p_sl23_z
        report.PSl23NonEu.Should().Be(435.96m);
        report.DanPSl23NonEu.Should().Be(Math.Round(435.96m * 0.21m, 2, MidpointRounding.AwayFromZero));
        // VetaC total
        report.CelkZdA2.Should().Be(176.5m + 435.96m);
        // Reverse-charge deduction (nárok)
        report.NarZdp23.Should().Be(176.5m + 435.96m);
    }

    [Fact]
    public async Task Build_InboundDomestic_SmallTotalGoesToB3Aggregate()
    {
        var userId = Guid.NewGuid();
        var invoices = new List<Invoice>
        {
            MakeInbound(userId, "FV-001", new DateTime(2026, 2, 10), 6660.11m, 1398.62m,
                supplierCountry: "CZ", supplierVatNo: "CZ12345678", total: 8058.73m),
        };

        var svc = new VatCalculationService(PassThroughFx().Object);
        var report = await svc.BuildAsync(invoices, OwnCompany(), 2026, 2);

        report.B2Rows.Should().BeEmpty();
        report.B3Aggregate.Base23.Should().Be(6660.11m);
        report.B3Aggregate.Dan23.Should().Be(1398.62m);
        report.Pln23.Should().Be(6660.11m);
        report.OdpTuz23.Should().Be(1398.62m);
    }

    [Fact]
    public async Task Build_UsesCnbConversion_ForForeignCurrencyReverseCharge()
    {
        var userId = Guid.NewGuid();
        var invoice = MakeInbound(userId, "AWS-001", new DateTime(2026, 2, 15), 100m, 0m,
            supplierCountry: "IE", supplierVatNo: "IE9999", currency: "EUR");

        var fx = new Mock<ICnbExchangeRateService>();
        fx.Setup(s => s.ConvertToCzkAsync(100m, "EUR", new DateOnly(2026, 2, 15), It.IsAny<CancellationToken>()))
          .ReturnsAsync(2500m);

        var svc = new VatCalculationService(fx.Object);
        var report = await svc.BuildAsync(new[] { invoice }, OwnCompany(), 2026, 2);

        report.A2Rows.Should().HaveCount(1);
        report.A2Rows[0].Base23.Should().Be(2500m);
        report.A2Rows[0].Dan23.Should().Be(525m);
        report.PSl23Eu.Should().Be(2500m);
    }

    [Fact]
    public async Task BuildQuarterAsync_GroupsThreeMonths()
    {
        var userId = Guid.NewGuid();
        var invoices = new List<Invoice>
        {
            MakeOutbound(userId, "Q2-A", new DateTime(2026, 4, 5), 1000m, 210m, clientDic: "06990258", total: 12000m),
            MakeOutbound(userId, "Q2-B", new DateTime(2026, 5, 20), 2000m, 420m, clientDic: "06990258", total: 12000m),
            MakeOutbound(userId, "Q2-C", new DateTime(2026, 6, 30), 3000m, 630m, clientDic: "06990258", total: 12000m),
        };

        var svc = new VatCalculationService(PassThroughFx().Object);
        var report = await svc.BuildQuarterAsync(invoices, OwnCompany(), 2026, 2);

        report.IsQuarterly.Should().BeTrue();
        report.Quarter.Should().Be(2);
        report.Month.Should().Be(4);
        report.Obrat23.Should().Be(6000m);
        report.Dan23.Should().Be(1260m);
        report.A4Rows.Should().HaveCount(3);
    }

    private static Invoice MakeOutbound(Guid userId, string number, DateTime duzp, decimal base21, decimal vat21,
        string? clientDic = null, decimal? total = null, string currency = "CZK")
    {
        var t = total ?? (base21 + vat21);
        return new Invoice
        {
            Id = Guid.NewGuid(),
            CompanyId = userId,
            Direction = InvoiceDirection.Outbound,
            Source = InvoiceSource.Manual,
            Number = number,
            DocumentType = "invoice",
            Status = "paid",
            Currency = currency,
            TaxableSupplyDate = duzp,
            IssuedOn = duzp,
            Subtotal = base21,
            Total = t,
            ClientCountry = "CZ",
            ClientVatNo = clientDic,
            VatRatesSummary = $"[{{\"vat_rate\":21,\"base\":{F(base21)},\"vat\":{F(vat21)}}}]",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ImportedAt = DateTime.UtcNow,
        };
    }

    private static Invoice MakeInbound(Guid userId, string number, DateTime duzp, decimal base21, decimal vat21,
        string supplierCountry = "CZ", string? supplierVatNo = null, decimal? total = null, string currency = "CZK")
    {
        var t = total ?? (base21 + vat21);
        return new Invoice
        {
            Id = Guid.NewGuid(),
            CompanyId = userId,
            Direction = InvoiceDirection.Inbound,
            Source = InvoiceSource.Manual,
            Number = number,
            DocumentType = "invoice",
            Status = "paid",
            Currency = currency,
            TaxableSupplyDate = duzp,
            IssuedOn = duzp,
            Subtotal = base21,
            Total = t,
            SupplierCountry = supplierCountry,
            SupplierVatNo = supplierVatNo,
            VatRatesSummary = vat21 == 0
                ? "[]"
                : $"[{{\"vat_rate\":21,\"base\":{F(base21)},\"vat\":{F(vat21)}}}]",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ImportedAt = DateTime.UtcNow,
        };
    }

    private static string F(decimal d) => d.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
