using FluentAssertions;
using Isdocovac.Models;
using Isdocovac.Models.Enums;
using Isdocovac.Services.Vat;

namespace Isdocovac.Tests.Services.Vat;

public class VatDateValidatorTests
{
    [Fact]
    public void Outbound_WithoutDuzp_IsMissing()
    {
        var inv = new Invoice { Direction = InvoiceDirection.Outbound, TaxableSupplyDate = null };
        VatDateValidator.Check(inv).Should().Be(DuzpRequirement.Missing);
        VatDateValidator.HasMissingRequiredDuzp(inv).Should().BeTrue();
    }

    [Fact]
    public void Outbound_WithDuzp_IsRequiredAndPresent()
    {
        var inv = new Invoice { Direction = InvoiceDirection.Outbound, TaxableSupplyDate = DateTime.UtcNow };
        VatDateValidator.Check(inv).Should().Be(DuzpRequirement.Required);
        VatDateValidator.HasMissingRequiredDuzp(inv).Should().BeFalse();
    }

    [Fact]
    public void Inbound_FromVatPayer_WithoutDuzp_IsMissing()
    {
        var inv = new Invoice
        {
            Direction = InvoiceDirection.Inbound,
            SupplierVatNo = "CZ12345678",
            TaxableSupplyDate = null,
        };
        VatDateValidator.HasMissingRequiredDuzp(inv).Should().BeTrue();
    }

    [Fact]
    public void Inbound_FromNonVatPayer_WithoutDuzp_IsNotRequired()
    {
        var inv = new Invoice
        {
            Direction = InvoiceDirection.Inbound,
            SupplierVatNo = null,
            TaxableSupplyDate = null,
        };
        VatDateValidator.Check(inv).Should().Be(DuzpRequirement.NotRequired);
        VatDateValidator.HasMissingRequiredDuzp(inv).Should().BeFalse();
    }

    [Fact]
    public void Inbound_FromVatPayer_WithDuzp_IsOk()
    {
        var inv = new Invoice
        {
            Direction = InvoiceDirection.Inbound,
            SupplierVatNo = "CZ12345678",
            TaxableSupplyDate = DateTime.UtcNow,
        };
        VatDateValidator.HasMissingRequiredDuzp(inv).Should().BeFalse();
    }
}
