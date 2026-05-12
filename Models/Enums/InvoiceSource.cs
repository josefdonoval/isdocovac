namespace Isdocovac.Models.Enums;

public enum InvoiceSource
{
    Fakturoid = 1,  // Synced from Fakturoid API
    ISDOC = 2,      // Imported from ISDOC XML upload
    Manual = 3,     // Manually created by user
    Email = 4       // Ingested from an email mailbox (IMAP)
}
