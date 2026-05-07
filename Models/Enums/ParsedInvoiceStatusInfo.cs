namespace Isdocovac.Models.Enums;

/// <summary>
/// Centralizes UI presentation for <see cref="ParsedInvoiceStatus"/> values:
/// Czech label, Bootstrap badge class, and a one-line description for tooltips.
///
/// Workflow recap (in document order):
///   Uploaded         → file is in storage, parser hasn't run yet.
///   Parsing          → parser/extractor is currently running.
///   ValidationFailed → parser couldn't produce usable data (XSD/extraction error).
///   Parsed           → data extracted; the user should review it.
///                      <see cref="ParsedInvoice.IsValid"/> is a sub-flag here:
///                      false means parsing succeeded but business validation flagged issues.
///   ReadyToImport    → user explicitly approved the parsed data; awaiting import click.
///   Imported         → mapped to a real Invoice row; the parsed record is now an audit trail.
/// </summary>
public static class ParsedInvoiceStatusInfo
{
    public static string Label(ParsedInvoiceStatus status, bool isValid = true) => status switch
    {
        ParsedInvoiceStatus.Uploaded => "Nahráno",
        ParsedInvoiceStatus.Parsing => "Zpracovává se",
        ParsedInvoiceStatus.Parsed => isValid ? "K revizi" : "K revizi (s chybami)",
        ParsedInvoiceStatus.ValidationFailed => "Chyba zpracování",
        ParsedInvoiceStatus.ReadyToImport => "Připraveno k importu",
        ParsedInvoiceStatus.Imported => "Naimportováno",
        _ => status.ToString(),
    };

    public static string BadgeCssClass(ParsedInvoiceStatus status, bool isValid = true) => status switch
    {
        ParsedInvoiceStatus.Uploaded => "bg-secondary",
        ParsedInvoiceStatus.Parsing => "bg-info",
        ParsedInvoiceStatus.Parsed => isValid ? "bg-primary" : "bg-warning text-dark",
        ParsedInvoiceStatus.ValidationFailed => "bg-danger",
        ParsedInvoiceStatus.ReadyToImport => "bg-warning text-dark",
        ParsedInvoiceStatus.Imported => "bg-success",
        _ => "bg-secondary",
    };

    public static string Description(ParsedInvoiceStatus status, bool isValid = true) => status switch
    {
        ParsedInvoiceStatus.Uploaded => "Soubor je uložen, čeká se na zpracování.",
        ParsedInvoiceStatus.Parsing => "Probíhá extrakce dat z dokumentu.",
        ParsedInvoiceStatus.Parsed => isValid
            ? "Data byla úspěšně rozpoznána. Zkontrolujte je a importujte."
            : "Data byla rozpoznána, ale obsahují validační chyby. Před importem zkontrolujte.",
        ParsedInvoiceStatus.ValidationFailed => "Zpracování selhalo. Dokument se nepodařilo rozpoznat — můžete spustit nové zpracování.",
        ParsedInvoiceStatus.ReadyToImport => "Data byla zkontrolována a schválena pro import. Stačí kliknout na Import.",
        ParsedInvoiceStatus.Imported => "Faktura byla úspěšně přenesena do systému a je dostupná v seznamu faktur.",
        _ => string.Empty,
    };
}
