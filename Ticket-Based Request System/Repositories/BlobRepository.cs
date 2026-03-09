using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace Ticket_Based_Request_System.Repositories
{
    public class BlobRepository
    {
        private readonly BlobContainerClient _container;

        public BlobRepository(IConfiguration configuration)
        {
            var blobServiceClient = new BlobServiceClient(
                configuration["BlobStorage:ConnectionString"]);

            _container = blobServiceClient.GetBlobContainerClient(
                configuration["BlobStorage:ContainerName"]);
        }

        public async Task<string> UploadAsync(
            string blobPath,
            Stream fileStream,
            string contentType)
        {
            var blobClient = _container.GetBlobClient(blobPath);

            await blobClient.UploadAsync(fileStream, overwrite: true);

            return blobClient.Uri.ToString();
        }
    }
}