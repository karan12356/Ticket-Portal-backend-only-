using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Ticket_Based_Request_System.Helpers;
using Ticket_Based_Request_System.Services;
using Microsoft.Azure.Cosmos;
using AppUser = Ticket_Based_Request_System.Models.User;

namespace Ticket_Based_Request_System.Functions.Auth
{
    public class Login
    {
        private readonly CosmosDbService _cosmos;

        public Login(CosmosDbService cosmos)
        {
            _cosmos = cosmos;
        }

        [Function("Login")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/login")]
            HttpRequestData req)
        {
            try
            {
                var body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body);

                string email = body.GetProperty("email").GetString();
                string password = body.GetProperty("password").GetString();

                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.email = @email")
                    .WithParameter("@email", email);

                var iterator = _cosmos.Users.GetItemQueryIterator<AppUser>(query);

                if (!iterator.HasMoreResults)
                    return req.CreateResponse(HttpStatusCode.Unauthorized);

                var response = await iterator.ReadNextAsync();
                var user = response.FirstOrDefault();

                if (user == null)
                    return req.CreateResponse(HttpStatusCode.Unauthorized);

                if (!PasswordHelper.Verify(password, user.passwordHash))
                    return req.CreateResponse(HttpStatusCode.Unauthorized);

                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(new
                {
                    userId = user.id,
                    user.employeeCode,
                    user.role,
                    user.rolePrefix
                });

                return res;
            }
            catch
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}
