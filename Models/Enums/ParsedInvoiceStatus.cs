namespace Isdocovac.Models.Enums;

public enum ParsedInvoiceStatus
{
    Uploaded = 10,          // File uploaded, not yet parsed
    Parsing = 20,           // Currently being parsed
    Parsed = 30,            // Successfully parsed, ready for review
    ValidationFailed = 40,  // Parse failed with validation errors
    ReadyToImport = 50,     // User reviewed and approved for import
    Imported = 60           // Successfully imported to Invoice table
}
