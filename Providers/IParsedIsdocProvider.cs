using Isdocovac.Models;

namespace Isdocovac.Providers;

public interface IParsedIsdocProvider
{
    Task<ParsedIsdoc> CreateParsedIsdocAsync(Guid invoiceUploadId, string parsedData, bool isValid, string? validationErrors = null);
    Task<IEnumerable<ParsedIsdoc>> GetParsedIsdocsForUploadAsync(Guid invoiceUploadId);
    Task<ParsedIsdoc?> GetLatestParsedIsdocForUploadAsync(Guid invoiceUploadId);
    Task<ParsedIsdoc?> GetParsedIsdocByIdAsync(Guid parsedIsdocId);
    Task UpdateParsedIsdocAsync(ParsedIsdoc parsedIsdoc);
    Task DeleteParsedIsdocAsync(Guid parsedIsdocId);
}
