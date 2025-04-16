
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace CallingService.Services
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
}
