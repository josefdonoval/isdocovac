using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace Isdocovac.Providers;

public interface IAzureBlobStorageProvider
{
    /// <summary>
    /// Uploads a file to Azure Blob Storage
    /// </summary>
    /// <param name="containerName">The blob container name</param>
    /// <param name="blobName">The blob name (file name in storage)</param>
    /// <param name="content">The file content as a stream</param>
    /// <param name="contentType">The MIME type of the file</param>
    /// <returns>The URL of the uploaded blob</returns>
    Task<string> UploadBlobAsync(
        string containerName,
        string blobName,
        Stream content,
        string contentType);

    /// <summary>
    /// Downloads a file from Azure Blob Storage
    /// </summary>
    /// <param name="containerName">The blob container name</param>
    /// <param name="blobName">The blob name</param>
    /// <returns>The file content as a stream</returns>
    Task<Stream> DownloadBlobAsync(
        string containerName,
        string blobName);

    /// <summary>
    /// Deletes a file from Azure Blob Storage
    /// </summary>
    /// <param name="containerName">The blob container name</param>
    /// <param name="blobName">The blob name</param>
    Task DeleteBlobAsync(
        string containerName,
        string blobName);

    /// <summary>
    /// Checks if a blob exists in Azure Blob Storage
    /// </summary>
    /// <param name="containerName">The blob container name</param>
    /// <param name="blobName">The blob name</param>
    /// <returns>True if the blob exists, false otherwise</returns>
    Task<bool> BlobExistsAsync(
        string containerName,
        string blobName);

    /// <summary>
    /// Gets the URL for a specific blob
    /// </summary>
    /// <param name="containerName">The blob container name</param>
    /// <param name="blobName">The blob name</param>
    /// <returns>The blob URL</returns>
    string GetBlobUrl(
        string containerName,
        string blobName);

    /// <summary>
    /// Generates a Shared Access Signature (SAS) URL for temporary access to a blob
    /// </summary>
    /// <param name="containerName">The blob container name</param>
    /// <param name="blobName">The blob name</param>
    /// <param name="expirationMinutes">How long the SAS URL should be valid (in minutes)</param>
    /// <returns>A SAS URL that provides temporary access to the blob</returns>
    Task<string> GenerateSasUrlAsync(
        string containerName,
        string blobName,
        int expirationMinutes = 60);
}

public class AzureBlobStorageProvider : IAzureBlobStorageProvider
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<AzureBlobStorageProvider> _logger;

    public AzureBlobStorageProvider(
        IConfiguration configuration,
        ILogger<AzureBlobStorageProvider> logger)
    {
        var connectionString = configuration["AzureStorage:ConnectionString"];
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Azure Storage connection string is not configured in appsettings.json");
        }

        _blobServiceClient = new BlobServiceClient(connectionString);
        _logger = logger;
    }

    public async Task<string> UploadBlobAsync(
        string containerName,
        string blobName,
        Stream content,
        string contentType)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

            // Create container if it doesn't exist
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

            var blobClient = containerClient.GetBlobClient(blobName);

            // Upload the blob
            await blobClient.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType });

            _logger.LogInformation("Successfully uploaded blob {BlobName} to container {ContainerName}", blobName,
                containerName);

            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading blob {BlobName} to container {ContainerName}", blobName,
                containerName);
            throw;
        }
    }

    public async Task<Stream> DownloadBlobAsync(
        string containerName,
        string blobName)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            var response = await blobClient.DownloadAsync();
            _logger.LogInformation("Successfully downloaded blob {BlobName} from container {ContainerName}", blobName,
                containerName);

            return response.Value.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading blob {BlobName} from container {ContainerName}", blobName,
                containerName);
            throw;
        }
    }

    public async Task DeleteBlobAsync(
        string containerName,
        string blobName)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            await blobClient.DeleteIfExistsAsync();
            _logger.LogInformation("Successfully deleted blob {BlobName} from container {ContainerName}", blobName,
                containerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting blob {BlobName} from container {ContainerName}", blobName,
                containerName);
            throw;
        }
    }

    public async Task<bool> BlobExistsAsync(
        string containerName,
        string blobName)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            return await blobClient.ExistsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence of blob {BlobName} in container {ContainerName}", blobName,
                containerName);
            throw;
        }
    }

    public string GetBlobUrl(
        string containerName,
        string blobName)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        return blobClient.Uri.ToString();
    }

    public async Task<string> GenerateSasUrlAsync(
        string containerName,
        string blobName,
        int expirationMinutes = 60)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            // Check if blob exists
            if (!await blobClient.ExistsAsync())
            {
                throw new InvalidOperationException($"Blob {blobName} does not exist in container {containerName}");
            }

            // Generate SAS token
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = blobName,
                Resource = "b", // "b" for blob
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5), // Small buffer for clock skew
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(expirationMinutes)
            };

            // Set permissions (read only)
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            // Generate the SAS URI
            var sasUri = blobClient.GenerateSasUri(sasBuilder);

            _logger.LogInformation(
                "Generated SAS URL for blob {BlobName} in container {ContainerName}, expires at {ExpiresOn}",
                blobName, containerName, sasBuilder.ExpiresOn);

            return sasUri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating SAS URL for blob {BlobName} in container {ContainerName}", blobName,
                containerName);
            throw;
        }
    }
}