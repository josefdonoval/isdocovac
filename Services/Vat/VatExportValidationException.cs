namespace Isdocovac.Services.Vat;

public class VatExportValidationException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public VatExportValidationException(IReadOnlyList<string> errors)
        : base("VAT export validation failed: " + string.Join("; ", errors))
    {
        Errors = errors;
    }
}
