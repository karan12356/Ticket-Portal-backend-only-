using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Ticket_Based_Request_System.Services;
using AppUser = Ticket_Based_Request_System.Models.User;

namespace Ticket_Based_Request_System.Functions.Profile
{
    public class UpdateDisplayName
    {
        private readonly CosmosDbService _cosmos;
        private readonly ILogger<UpdateDisplayName> _logger;

        public UpdateDisplayName(CosmosDbService cosmos, ILogger<UpdateDisplayName> logger)
        {
            _cosmos = cosmos;
            _logger = logger;
        }

        [Function("UpdateDisplayName")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "users/{email}/update-name")]
            HttpRequestData req,
            string email)
        {
            try
            {
                _logger.LogInformation(
                    "UpdateDisplayName API triggered for Email: {Email}",
                    email);

                var body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body);
                string newName = body.GetProperty("name").GetString();

                if (string.IsNullOrWhiteSpace(newName))
                {
                    _logger.LogWarning(
                        "Display name update failed. Name is empty. Email: {Email}",
                        email);

                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

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
                        "Display name update failed. User not found. Email: {Email}",
                        email);

                    return req.CreateResponse(HttpStatusCode.NotFound);
                }

                _logger.LogInformation(
                    "Updating display name for Email: {Email}. NewName: {NewName}",
                    email, newName);

                user.name = newName;

                await container.ReplaceItemAsync(
                    user,
                    user.id,
                    new PartitionKey(user.email));

                _logger.LogInformation(
                    "Display name updated successfully for Email: {Email}",
                    email);

                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteStringAsync("Display name updated successfully");
                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error occurred while updating display name for Email: {Email}",
                    email);

                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}