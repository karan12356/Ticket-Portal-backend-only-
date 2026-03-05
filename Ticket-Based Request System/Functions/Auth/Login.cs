using Microsoft.Extensions.Logging;
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
        private readonly ILogger<Login> _logger;

        public Login(CosmosDbService cosmos, ILogger<Login> logger)
        {
            _cosmos = cosmos;
            _logger = logger;
        }

        [Function("Login")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/login")]
            HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("Login API triggered.");

                var body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body);

                string email = body.GetProperty("email").GetString();
                string password = body.GetProperty("password").GetString();

                _logger.LogInformation("Login attempt for email: {Email}", email);

                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.email = @email")
                    .WithParameter("@email", email);

                var iterator = _cosmos.Users.GetItemQueryIterator<AppUser>(query);

                if (!iterator.HasMoreResults)
                {
                    _logger.LogWarning("Login failed. No results found for email: {Email}", email);
                    return req.CreateResponse(HttpStatusCode.Unauthorized);
                }

                var response = await iterator.ReadNextAsync();
                var user = response.FirstOrDefault();

                if (user == null)
                {
                    _logger.LogWarning("Login failed. User not found for email: {Email}", email);
                    return req.CreateResponse(HttpStatusCode.Unauthorized);
                }

                if (!PasswordHelper.Verify(password, user.passwordHash))
                {
                    _logger.LogWarning("Invalid password attempt for email: {Email}", email);
                    return req.CreateResponse(HttpStatusCode.Unauthorized);
                }

                _logger.LogInformation(
                    "User login successful. UserId: {UserId}, Role: {Role}, EmployeeCode: {EmployeeCode}",
                    user.id,
                    user.role,
                    user.employeeCode
                );

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during login process.");
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}