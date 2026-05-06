using Isdocovac.Models.Enums;
using Isdocovac.Models.Extraction;

namespace Isdocovac.Services.Claude;

public interface IInvoiceParsingService
{
    Task<string> UploadPdfAsync(Stream pdfStream, string filename);
    Task<InvoiceExtractionResult> ExtractInvoiceDataAsync(string fileId, InvoiceLineMode lineMode);
    Task DeleteFileAsync(string fileId);
}
