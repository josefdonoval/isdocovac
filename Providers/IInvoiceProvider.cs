using Isdocovac.Models;
using Isdocovac.Models.Enums;

namespace Isdocovac.Providers;

public interface IInvoiceProvider
{
    Task<InvoiceUpload> CreateUploadAsync(Guid userId, string fileName, long fileSize, string contentType, string xmlContent);
    Task<IEnumerable<InvoiceUpload>> GetUserUploadsAsync(Guid userId);
    Task<InvoiceUpload?> GetUploadByIdAsync(Guid uploadId);
    Task<InvoiceUpload?> GetUploadWithParsedDataAsync(Guid uploadId);
    Task UpdateUploadStatusAsync(Guid uploadId, InvoiceUploadStatus status);
    Task DeleteUploadAsync(Guid uploadId);
}
