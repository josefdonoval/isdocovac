using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Isdocovac.Models.Vat;
using Isdocovac.Services.Fx;

namespace Isdocovac.Services.Vat;

public interface IVatCalculationService
{
    Task<VatMonthlyReport> BuildAsync(
        IEnumerable<Invoice> invoices,
        Contact ownCompany,
        int year,
        int month,
        CancellationToken ct = default);
}

public class VatCalculationService : IVatCalculationService
{
    private const decimal StandardRate = 21m;
    private const decimal ReducedRate = 12m;
    private const decimal SmallInvoiceThresholdCzk = 10_000m;

    private readonly ICnbExchangeRateService _fx;

    public VatCalculationService(ICnbExchangeRateService fx)
    {
        _fx = fx;
    }

    public async Task<VatMonthlyReport> BuildAsync(
        IEnumerable<Invoice> invoices,
        Contact ownCompany,
        int year,
        int month,
        CancellationToken ct = default)
    {
        var errors = new List<string>();
        var report = new VatMonthlyReport
        {
            Year = year,
            Month = month,
            OwnCompany = ownCompany,
        };

        foreach (var inv in invoices)
        {
            if (!inv.TaxableSupplyDate.HasValue)
            {
                errors.Add($"Faktura {inv.Number}: chybí DUZP (TaxableSupplyDate).");
                continue;
            }

            var rates = VatRatesParser.Parse(inv.VatRatesSummary);

            try
            {
                if (inv.Direction == InvoiceDirection.Outbound)
                {
                    await ProcessOutboundAsync(inv, rates, report, ct);
                }
                else if (inv.Direction == InvoiceDirection.Inbound)
                {
                    await ProcessInboundAsync(inv, rates, report, ct);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Faktura {inv.Number}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            throw new VatExportValidationException(errors);
        }

        ComputeControlTotals(report);
        return report;
    }

    private async Task ProcessOutboundAsync(
        Invoice inv,
        IReadOnlyList<VatRateLine> rates,
        VatMonthlyReport report,
        CancellationToken ct)
    {
        var isDomestic = string.Equals(inv.ClientCountry, "CZ", StringComparison.OrdinalIgnoreCase)
                         || string.IsNullOrWhiteSpace(inv.ClientCountry);
        if (!isDomestic)
        {
            // Out of v1 scope — but do not leak into Veta1.
            return;
        }

        var (base21, vat21) = SumRate(rates, StandardRate);
        var (base12, vat12) = SumRate(rates, ReducedRate);

        base21 = await ToCzkAsync(base21, inv, ct);
        vat21 = await ToCzkAsync(vat21, inv, ct);
        base12 = await ToCzkAsync(base12, inv, ct);
        vat12 = await ToCzkAsync(vat12, inv, ct);

        report.Obrat23 += base21;
        report.Dan23 += vat21;
        report.Obrat5 += base12;
        report.Dan5 += vat12;

        var totalCzk = await ToCzkAsync(inv.Total, inv, ct);
        if (totalCzk > SmallInvoiceThresholdCzk)
        {
            report.A4Rows.Add(new KhA4Row(
                inv.Number,
                inv.TaxableSupplyDate!.Value,
                VatNumber.StripCountryPrefix(inv.ClientVatNo),
                base21, vat21, base12, vat12));
        }
        else
        {
            report.A5Aggregate.Base23 += base21;
            report.A5Aggregate.Dan23 += vat21;
            report.A5Aggregate.Base5 += base12;
            report.A5Aggregate.Dan5 += vat12;
        }
    }

    private async Task ProcessInboundAsync(
        Invoice inv,
        IReadOnlyList<VatRateLine> rates,
        VatMonthlyReport report,
        CancellationToken ct)
    {
        var country = inv.SupplierCountry ?? string.Empty;
        var isDomestic = string.Equals(country, "CZ", StringComparison.OrdinalIgnoreCase)
                         || string.IsNullOrWhiteSpace(country);

        if (isDomestic)
        {
            var (base21, vat21) = SumRate(rates, StandardRate);
            var (base12, vat12) = SumRate(rates, ReducedRate);

            base21 = await ToCzkAsync(base21, inv, ct);
            vat21 = await ToCzkAsync(vat21, inv, ct);
            base12 = await ToCzkAsync(base12, inv, ct);
            vat12 = await ToCzkAsync(vat12, inv, ct);

            report.Pln23 += base21;
            report.OdpTuz23 += vat21;
            report.Pln5 += base12;
            report.OdpTuz5 += vat12;

            var totalCzk = await ToCzkAsync(inv.Total, inv, ct);
            if (totalCzk > SmallInvoiceThresholdCzk)
            {
                report.B2Rows.Add(new KhB2Row(
                    inv.Number,
                    inv.TaxableSupplyDate!.Value,
                    VatNumber.StripCountryPrefix(inv.SupplierVatNo),
                    base21, vat21, base12, vat12));
            }
            else
            {
                report.B3Aggregate.Base23 += base21;
                report.B3Aggregate.Dan23 += vat21;
                report.B3Aggregate.Base5 += base12;
                report.B3Aggregate.Dan5 += vat12;
            }
            return;
        }

        // Foreign supplier — reverse charge.
        // Convert the invoice Subtotal to CZK and self-assess VAT at 21% (v1 assumption).
        var supplyDate = DateOnly.FromDateTime(inv.TaxableSupplyDate!.Value);
        var subtotalCzk = await _fx.ConvertToCzkAsync(inv.Subtotal, inv.Currency, supplyDate, ct);
        var vatCzk = Math.Round(subtotalCzk * StandardRate / 100m, 2, MidpointRounding.AwayFromZero);
        var base21Rc = subtotalCzk;
        var vat21Rc = vatCzk;
        var base12Rc = 0m;
        var vat12Rc = 0m;

        // Input VAT deduction mirrors the self-assessed output (standard full deduction).
        report.NarZdp23 += base21Rc;
        report.OdZdp23 += vat21Rc;

        // Route into Veta1 depending on EU vs non-EU supplier country.
        if (EuCountries.IsEu(country))
        {
            report.PSl23Eu += base21Rc;
            report.DanPSl23Eu += vat21Rc;
            report.PSl5Eu += base12Rc;
            report.DanPSl5Eu += vat12Rc;
        }
        else
        {
            report.PSl23NonEu += base21Rc;
            report.DanPSl23NonEu += vat21Rc;
            report.PSl5NonEu += base12Rc;
            report.DanPSl5NonEu += vat12Rc;
        }

        report.A2Rows.Add(new KhA2Row(
            string.IsNullOrWhiteSpace(inv.Number) ? inv.Id.ToString("N")[..8] : inv.Number,
            inv.TaxableSupplyDate!.Value,
            string.IsNullOrWhiteSpace(country) ? "ZZ" : country.ToUpperInvariant(),
            VatNumber.StripCountryPrefix(inv.SupplierVatNo),
            base21Rc, vat21Rc, base12Rc, vat12Rc));
    }

    private static (decimal baseAmt, decimal vat) SumRate(IReadOnlyList<VatRateLine> rates, decimal targetRate)
    {
        decimal b = 0, v = 0;
        foreach (var r in rates)
        {
            if (r.VatRate == targetRate)
            {
                b += r.Base;
                v += r.Vat;
            }
        }
        return (b, v);
    }

    private async Task<decimal> ToCzkAsync(decimal amount, Invoice inv, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(inv.Currency)
            || string.Equals(inv.Currency, "CZK", StringComparison.OrdinalIgnoreCase))
        {
            return amount;
        }
        var date = DateOnly.FromDateTime(inv.TaxableSupplyDate ?? inv.IssuedOn ?? DateTime.UtcNow);
        return await _fx.ConvertToCzkAsync(amount, inv.Currency, date, ct);
    }

    private static void ComputeControlTotals(VatMonthlyReport r)
    {
        r.CelkZdA2 = r.A2Rows.Sum(x => x.Base23 + x.Base5);
        r.KhObrat23 = r.A4Rows.Sum(x => x.Base23) + r.A5Aggregate.Base23;
        r.KhObrat5 = r.A4Rows.Sum(x => x.Base5) + r.A5Aggregate.Base5;
        r.KhPln23 = r.B2Rows.Sum(x => x.Base23) + r.B3Aggregate.Base23;
        r.KhPln5 = r.B2Rows.Sum(x => x.Base5) + r.B3Aggregate.Base5;
    }
}
