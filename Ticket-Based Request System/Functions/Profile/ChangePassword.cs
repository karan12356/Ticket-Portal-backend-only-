using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Cosmos;
using Ticket_Based_Request_System.Services;
using Ticket_Based_Request_System.Helpers;
using AppUser = Ticket_Based_Request_System.Models.User;

namespace Ticket_Based_Request_System.Functions.Profile
{
    public class ChangePassword
    {
        private readonly CosmosDbService _cosmos;

        public ChangePassword(CosmosDbService cosmos)
        {
            _cosmos = cosmos;
        }

        [Function("ChangePassword")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "users/{email}/change-password")]
            HttpRequestData req,
            string email)
        {
            try
            {
                var body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body);

                string oldPassword = body.GetProperty("oldPassword").GetString();
                string newPassword = body.GetProperty("newPassword").GetString();

                if (string.IsNullOrWhiteSpace(oldPassword) ||
                    string.IsNullOrWhiteSpace(newPassword))
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

                if (!PasswordHelper.Verify(oldPassword, user.passwordHash))
                    return req.CreateResponse(HttpStatusCode.Unauthorized);

                user.passwordHash = PasswordHelper.HashPassword(newPassword);

                await container.ReplaceItemAsync(
                    user,
                    user.id,
                    new PartitionKey(user.email));

                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteStringAsync("Password changed successfully");
                return res;
            }
            catch
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}
