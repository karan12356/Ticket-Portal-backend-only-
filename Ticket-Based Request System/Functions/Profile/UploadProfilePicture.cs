using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Ticket_Based_Request_System.Services;
using AppUser = Ticket_Based_Request_System.Models.User;

namespace Ticket_Based_Request_System.Functions.Profile
{
    public class UploadProfilePicture
    {
        private readonly CosmosDbService _cosmos;
        private readonly BlobStorageService _blob;
        private readonly ILogger<UploadProfilePicture> _logger;

        public UploadProfilePicture(
            CosmosDbService cosmos,
            BlobStorageService blob,
            ILogger<UploadProfilePicture> logger)
        {
            _cosmos = cosmos;
            _blob = blob;
            _logger = logger;
        }

        [Function("UploadProfilePicture")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post",
            Route = "users/{email}/upload-profile")]
            HttpRequestData req,
            string email)
        {
            try
            {
                _logger.LogInformation(
                    "UploadProfilePicture API triggered for Email: {Email}",
                    email);

                var container = _cosmos.Users;

                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.email = @email")
                    .WithParameter("@email", email);

                var iterator = container.GetItemQueryIterator<AppUser>(query);
                var result = await iterator.ReadNextAsync();
                var user = result.FirstOrDefault();

                if (user == null)
                {
                    _logger.LogWarning(
                        "Profile upload failed. User not found. Email: {Email}",
                        email);

                    return req.CreateResponse(HttpStatusCode.NotFound);
                }

                _logger.LogInformation(
                    "User found for profile upload. UserId: {UserId}",
                    user.id);

                var contentType = req.Headers
                    .GetValues("Content-Type").First();

                var boundary = HeaderUtilities
                    .RemoveQuotes(
                        MediaTypeHeaderValue.Parse(contentType).Boundary)
                    .Value;

                var reader = new MultipartReader(boundary, req.Body);
                var section = await reader.ReadNextSectionAsync();

                while (section != null)
                {
                    var fileName = $"{user.id}.jpg";
                    var blobPath = $"profiles/{fileName}";

                    _logger.LogInformation(
                        "Uploading profile picture to blob storage. BlobPath: {BlobPath}",
                        blobPath);

                    var blobUrl = await _blob.UploadAsync(
                        blobPath,
                        section.Body,
                        "image/jpeg");

                    user.profileImageUrl = blobUrl;

                    await container.ReplaceItemAsync(
                        user,
                        user.id,
                        new PartitionKey(user.email));

                    _logger.LogInformation(
                        "Profile image uploaded successfully. UserId: {UserId}",
                        user.id);

                    var res = req.CreateResponse(HttpStatusCode.OK);
                    await res.WriteAsJsonAsync(new
                    {
                        message = "Profile image uploaded successfully",
                        profileImageUrl = blobUrl
                    });

                    return res;
                }

                _logger.LogWarning(
                    "Profile upload failed. No file section found. Email: {Email}",
                    email);

                return req.CreateResponse(HttpStatusCode.BadRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error occurred during profile image upload. Email: {Email}",
                    email);

                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}