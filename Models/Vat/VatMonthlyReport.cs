namespace Isdocovac.Models.Vat;

public record VatRateBucket(decimal Standard, decimal Reduced)
{
    public static VatRateBucket Zero => new(0m, 0m);

    public VatRateBucket Add(decimal standard, decimal reduced) =>
        new(Standard + standard, Reduced + reduced);
}

public class VatMonthlyReport
{
    public required int Year { get; init; }
    public required int Month { get; init; }
    public required Contact OwnCompany { get; init; }

    // KH rows
    public List<KhA2Row> A2Rows { get; } = new();
    public List<KhA4Row> A4Rows { get; } = new();
    public KhAggregateRow A5Aggregate { get; set; } = new();
    public List<KhB2Row> B2Rows { get; } = new();
    public KhAggregateRow B3Aggregate { get; set; } = new();

    // DP totals
    public decimal Obrat23 { get; set; }
    public decimal Dan23 { get; set; }
    public decimal Obrat5 { get; set; }
    public decimal Dan5 { get; set; }

    // Reverse charge splits (service)
    public decimal PSl23Eu { get; set; }       // r. 5 base
    public decimal DanPSl23Eu { get; set; }    // r. 5 tax
    public decimal PSl5Eu { get; set; }        // r. 6 base
    public decimal DanPSl5Eu { get; set; }     // r. 6 tax
    public decimal PSl23NonEu { get; set; }    // r. 12 base
    public decimal DanPSl23NonEu { get; set; } // r. 12 tax
    public decimal PSl5NonEu { get; set; }     // r. 13 base
    public decimal DanPSl5NonEu { get; set; }  // r. 13 tax

    // Veta4 (input VAT)
    public decimal Pln23 { get; set; }
    public decimal OdpTuz23 { get; set; }
    public decimal Pln5 { get; set; }
    public decimal OdpTuz5 { get; set; }
    public decimal NarZdp23 { get; set; }
    public decimal OdZdp23 { get; set; }
    public decimal NarZdp5 { get; set; }
    public decimal OdZdp5 { get; set; }

    public decimal OdpSumNar => OdpTuz23 + OdpTuz5 + OdZdp23 + OdZdp5;
    public decimal DanZocelk => Dan23 + Dan5 + DanPSl23Eu + DanPSl5Eu + DanPSl23NonEu + DanPSl5NonEu;
    public decimal OdpZocelk => OdpSumNar;
    public decimal DanoDa => Math.Max(0, Math.Round(DanZocelk, 0, MidpointRounding.AwayFromZero) - Math.Round(OdpZocelk, 0, MidpointRounding.AwayFromZero));
    public decimal DanoNo => Math.Max(0, Math.Round(OdpZocelk, 0, MidpointRounding.AwayFromZero) - Math.Round(DanZocelk, 0, MidpointRounding.AwayFromZero));

    // VetaC (KH control totals — halíře)
    public decimal CelkZdA2 { get; set; }
    public decimal KhObrat23 { get; set; }   // A4 + A5
    public decimal KhObrat5 { get; set; }
    public decimal KhPln23 { get; set; }     // B2 + B3
    public decimal KhPln5 { get; set; }
}

public class KhAggregateRow
{
    public decimal Base23 { get; set; }
    public decimal Dan23 { get; set; }
    public decimal Base5 { get; set; }
    public decimal Dan5 { get; set; }
    public bool HasAnyValue => Base23 != 0 || Dan23 != 0 || Base5 != 0 || Dan5 != 0;
}

public record KhA2Row(
    string EvidenceNumber,
    DateTime TaxableSupplyDate,
    string CountryCode,
    string? VatId,
    decimal Base23,
    decimal Dan23,
    decimal Base5,
    decimal Dan5);

public record KhA4Row(
    string EvidenceNumber,
    DateTime TaxableSupplyDate,
    string? ClientDic,
    decimal Base23,
    decimal Dan23,
    decimal Base5,
    decimal Dan5);

public record KhB2Row(
    string EvidenceNumber,
    DateTime TaxableSupplyDate,
    string? SupplierDic,
    decimal Base23,
    decimal Dan23,
    decimal Base5,
    decimal Dan5);
