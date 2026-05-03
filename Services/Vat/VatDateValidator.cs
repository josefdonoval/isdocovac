using Isdocovac.Models;
using Isdocovac.Models.Enums;

namespace Isdocovac.Services.Vat;

public enum DuzpRequirement
{
    NotRequired,
    Required,
    Missing,
}

public static class VatDateValidator
{
    public static bool IsDuzpRequired(Invoice invoice)
    {
        if (invoice.Direction == InvoiceDirection.Outbound)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(invoice.SupplierVatNo);
    }

    public static DuzpRequirement Check(Invoice invoice)
    {
        var required = IsDuzpRequired(invoice);
        if (!required)
        {
            return DuzpRequirement.NotRequired;
        }
        return invoice.TaxableSupplyDate.HasValue
            ? DuzpRequirement.Required
            : DuzpRequirement.Missing;
    }

    public static bool HasMissingRequiredDuzp(Invoice invoice)
        => Check(invoice) == DuzpRequirement.Missing;
}
