using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Cosmos;
using Ticket_Based_Request_System.Services;
using AppUser = Ticket_Based_Request_System.Models.User;

namespace Ticket_Based_Request_System.Functions.Profile
{
    public class UpdateDisplayName
    {
        private readonly CosmosDbService _cosmos;

        public UpdateDisplayName(CosmosDbService cosmos)
        {
            _cosmos = cosmos;
        }

        [Function("UpdateDisplayName")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "users/{email}/update-name")]
            HttpRequestData req,
            string email)
        {
            try
            {
                var body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body);
                string newName = body.GetProperty("name").GetString();

                if (string.IsNullOrWhiteSpace(newName))
                    return req.CreateResponse(HttpStatusCode.BadRequest);

                var container = _cosmos.Users;

                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.email = @email")
                    .WithParameter("@email", email);

                var iterator = container.GetItemQueryIterator<AppUser>(query);
                var result = await iterator.ReadNextAsync();
                var user = result.FirstOrDefault();

                if (user == null)
                    return req.CreateResponse(HttpStatusCode.NotFound);

                user.name = newName;

                await container.ReplaceItemAsync(
                    user,
                    user.id,
                    new PartitionKey(user.email));

                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteStringAsync("Display name updated successfully");
                return res;
            }
            catch
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}
