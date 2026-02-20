using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Cosmos;
using Ticket_Based_Request_System.Services;
using Ticket_Based_Request_System.Helpers;

using AppUser = Ticket_Based_Request_System.Models.User;

namespace Ticket_Based_Request_System.Functions.Auth
{
    public class Signup
    {
        private readonly CosmosDbService _cosmos;

        public Signup(CosmosDbService cosmos)
        {
            _cosmos = cosmos;
        }

        [Function("Signup")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/signup")]
            HttpRequestData req)
        {
            try
            {
                var body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body);

                string name = body.GetProperty("name").GetString();
                string email = body.GetProperty("email").GetString();
                string password = body.GetProperty("password").GetString();
                string role = body.GetProperty("role").GetString();

                if (string.IsNullOrWhiteSpace(name) ||
                    string.IsNullOrWhiteSpace(email) ||
                    string.IsNullOrWhiteSpace(password) ||
                    string.IsNullOrWhiteSpace(role))
                {
                    return BadRequest(req, "All fields are required");
                }

                if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    return BadRequest(req, "Invalid email format");
                }

                if (!Regex.IsMatch(password,
                    @"^(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&]).{8,}$"))
                {
                    return BadRequest(req,
                        "Password must be at least 8 characters and include uppercase, number, and special character");
                }

                string rolePrefix = role switch
                {
                    "Sales Rep" => "W",
                    "SVP" => "X",
                    "IT Manager" => "Y",
                    "Admin" => "A",   
                    _ => null
                };


                if (rolePrefix == null)
                {
                    return BadRequest(req, "Invalid role");
                }

                var emailQuery = new QueryDefinition(
                    "SELECT VALUE COUNT(1) FROM c WHERE c.email = @email")
                    .WithParameter("@email", email);

                var iterator = _cosmos.Users.GetItemQueryIterator<int>(emailQuery);
                int emailCount = (await iterator.ReadNextAsync()).FirstOrDefault();

                if (emailCount > 0)
                {
                    var conflict = req.CreateResponse(HttpStatusCode.Conflict);
                    await conflict.WriteStringAsync("Email already exists");
                    return conflict;
                }

                var counter = await _cosmos.Counters.ReadItemAsync<dynamic>(
                    "employeeCode",
                    new PartitionKey("employee"));

                int nextCode = counter.Resource.currentValue + 1;
                counter.Resource.currentValue = nextCode;

                await _cosmos.Counters.ReplaceItemAsync(
                    counter.Resource,
                    "employeeCode",
                    new PartitionKey("employee"));

                var user = new AppUser
                {
                    name = name,
                    email = email,
                    employeeCode = nextCode.ToString("D3"),
                    role = role,
                    rolePrefix = rolePrefix,
                    passwordHash = PasswordHelper.HashPassword(password)
                };

                await _cosmos.Users.CreateItemAsync(
                    user,
                    new PartitionKey(email));

                var res = req.CreateResponse(HttpStatusCode.Created);
                await res.WriteAsJsonAsync(new
                {
                    user.id,
                    user.employeeCode,
                    user.name,
                    user.email,
                    user.role
                });

                return res;
            }
            catch
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        private HttpResponseData BadRequest(HttpRequestData req, string message)
        {
            var res = req.CreateResponse(HttpStatusCode.BadRequest);
            res.WriteString(message);
            return res;
        }
    }
}
