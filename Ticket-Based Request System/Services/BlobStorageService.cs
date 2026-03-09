using Ticket_Based_Request_System.Repositories;

namespace Ticket_Based_Request_System.Services
{
    public class BlobStorageService
    {
        private readonly BlobRepository _repository;

        public BlobStorageService(BlobRepository repository)
        {
            _repository = repository;
        }

        public async Task<string> UploadAsync(
            string blobPath,
            Stream fileStream,
            string contentType)
        {
            return await _repository.UploadAsync(
                blobPath,
                fileStream,
                contentType);
        }
    }
}