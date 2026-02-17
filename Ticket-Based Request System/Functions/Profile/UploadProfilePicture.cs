using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Ticket_Based_Request_System.Services;
using AppUser = Ticket_Based_Request_System.Models.User;

namespace Ticket_Based_Request_System.Functions.Profile
{
    public class UploadProfilePicture
    {
        private readonly CosmosDbService _cosmos;
        private readonly BlobStorageService _blob;

        public UploadProfilePicture(
            CosmosDbService cosmos,
            BlobStorageService blob)
        {
            _cosmos = cosmos;
            _blob = blob;
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
                var container = _cosmos.Users;

                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.email = @email")
                    .WithParameter("@email", email);

                var iterator = container.GetItemQueryIterator<AppUser>(query);
                var result = await iterator.ReadNextAsync();
                var user = result.FirstOrDefault();

                if (user == null)
                    return req.CreateResponse(HttpStatusCode.NotFound);

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

                    var blobUrl = await _blob.UploadAsync(
                        blobPath,
                        section.Body,
                        "image/jpeg");

                    user.profileImageUrl = blobUrl;

                    await container.ReplaceItemAsync(
                        user,
                        user.id,
                        new PartitionKey(user.email));

                    var res = req.CreateResponse(HttpStatusCode.OK);
                    await res.WriteAsJsonAsync(new
                    {
                        message = "Profile image uploaded successfully",
                        profileImageUrl = blobUrl
                    });

                    return res;
                }

                return req.CreateResponse(HttpStatusCode.BadRequest);
            }
            catch
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}
