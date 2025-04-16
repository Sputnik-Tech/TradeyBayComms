/* 
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace MessagingService.Services
{
    public class ImageStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;

        public ImageStorageService(IConfiguration configuration)
        {
            // Try to fetch the connection string from environment variables first
            string connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

            if (string.IsNullOrEmpty(connectionString))
            {
                // Fallback to configuration file (useful for local development)
                connectionString = configuration.GetSection("AzureStorage:ConnectionString").Value;

                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("Azure Storage Connection String is not set in environment variables or configuration.");
                }
            }

            _blobServiceClient = new BlobServiceClient(connectionString);
        }

        public async Task<string> UploadImageAsync(Stream imageStream, string fileName, string containerName, string contentType = "image/jpeg")
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();

            var blobClient = containerClient.GetBlobClient(fileName);
            await blobClient.UploadAsync(imageStream, new BlobHttpHeaders { ContentType = contentType });

            return blobClient.Uri.ToString();
        }
    }
} */


using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Azure.Identity; // For Managed Identity

namespace MessagingService.Services;

public interface IBlobStorageService
{
    Task<Uri> GetUploadSasUriAsync(string blobName, TimeSpan expiration);
    string GetDownloadUrl(string blobName);
}

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _containerClient;
    private readonly IConfiguration _configuration;

    public BlobStorageService(IConfiguration configuration)
    {
        _configuration = configuration;
        var connectionString = _configuration["BlobStorage:ConnectionString"];
        var containerName = _configuration["BlobStorage:ContainerName"];

        if (string.IsNullOrEmpty(containerName))
        {
            throw new ArgumentNullException(nameof(containerName),"Blob container name not configured.");
        }

        // TODO: Use DefaultAzureCredential for Managed Identity in production
        if (!string.IsNullOrEmpty(connectionString) && connectionString != "your_azure_blob_storage_connection_string") // Avoid using placeholder
        {
             _containerClient = new BlobContainerClient(connectionString, containerName);
        }
        else
        {
             // Attempt Managed Identity (will work in Azure environments like AKS with MI enabled)
             // Uri blobServiceUri = new Uri($"https://{_configuration["BlobStorage:AccountName"]}.blob.core.windows.net");
             // _containerClient = new BlobContainerClient(new Uri($"{blobServiceUri.AbsoluteUri}{containerName}"), new DefaultAzureCredential());
             // For now, throw if no connection string during local dev without MI setup
              throw new InvalidOperationException("Blob Storage Connection String is missing or invalid, and Managed Identity could not be used.");
        }

        _containerClient.CreateIfNotExists(); // Ensure container exists
    }

    public async Task<Uri> GetUploadSasUriAsync(string blobName, TimeSpan expiration)
    {
        BlobClient blobClient = _containerClient.GetBlobClient(blobName);

        if (!blobClient.CanGenerateSasUri)
        {
            // Handle scenario where SAS generation isn't possible (e.g., using User Delegation SAS without proper RBAC)
             throw new InvalidOperationException("Cannot generate SAS URI for the blob client.");
        }


        BlobSasBuilder sasBuilder = new BlobSasBuilder()
        {
            BlobContainerName = _containerClient.Name,
            BlobName = blobName,
            Resource = "b", // "b" for blob
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5), // Allow for clock skew
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiration),
        };

        // Specify permissions for the SAS token (Write for upload)
        sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create); // Add Create for potential new blobs

         // Use the Account Key for SAS generation (less secure, prefer User Delegation SAS in Prod if possible)
        Uri sasUri = blobClient.GenerateSasUri(sasBuilder);
        return sasUri;

        // TODO: Implement User Delegation SAS for better security in production
        // Requires BlobServiceClient, RBAC roles (Storage Blob Data Contributor) for the Managed Identity
        // var userDelegationKey = await _containerClient.GetUserDelegationKeyAsync(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.Add(expiration));
        // Uri sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Write, userDelegationKey, DateTimeOffset.UtcNow.Add(expiration));
        // return sasUri;
    }

     public string GetDownloadUrl(string blobName)
     {
         // Assumes the container is public or uses SAS for download if private
         // For private blobs, you'd generate a read-only SAS URL here similar to upload
         BlobClient blobClient = _containerClient.GetBlobClient(blobName);
         return blobClient.Uri.AbsoluteUri;
     }
}