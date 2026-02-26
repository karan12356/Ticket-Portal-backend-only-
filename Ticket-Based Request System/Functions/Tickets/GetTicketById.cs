using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using Ticket_Based_Request_System.Models;
using Ticket_Based_Request_System.Services;
using Ticket_Based_Request_System.Helpers;

using AppUser = Ticket_Based_Request_System.Models.User;

namespace Ticket_Based_Request_System.Functions.Tickets
{
    public class GetTicketById
    {
        private readonly CosmosDbService _cosmos;

        public GetTicketById(CosmosDbService cosmos)
        {
            _cosmos = cosmos;
        }

        [Function("GetTicketById")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "tickets/{id}")]
            HttpRequestData req,
            string id)
        {
            string userId = req.Query["userId"];
            string role = req.Query["role"];
            string adminUserId = req.Query["adminUserId"];
            string password = req.Query["password"];

            try
            {
                var response = await _cosmos.Tickets.ReadItemAsync<Ticket>(
                    id,
                    new PartitionKey(userId));

                var ticket = response.Resource;

                if (ticket.isConfidential)
                {
                    if (role != "Admin")
                    {
                        var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                        await unauthorized.WriteStringAsync("Only Admin can view confidential tickets.");
                        return unauthorized;
                    }

                    if (string.IsNullOrWhiteSpace(adminUserId) || string.IsNullOrWhiteSpace(password))
                    {
                        var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                        await badReq.WriteStringAsync("Admin credentials required.");
                        return badReq;
                    }

                    var query = new QueryDefinition(
                        "SELECT * FROM c WHERE c.id = @id")
                        .WithParameter("@id", adminUserId);

                    var iterator = _cosmos.Users.GetItemQueryIterator<AppUser>(query);

                    if (!iterator.HasMoreResults)
                        return req.CreateResponse(HttpStatusCode.Unauthorized);

                    var adminResponse = await iterator.ReadNextAsync();
                    var adminUser = adminResponse.FirstOrDefault();

                    if (adminUser == null)
                        return req.CreateResponse(HttpStatusCode.Unauthorized);

                    if (!PasswordHelper.Verify(password, adminUser.passwordHash))
                        return req.CreateResponse(HttpStatusCode.Unauthorized);

                    ticket.description = EncryptionHelper.Decrypt(ticket.description);
                }

                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(ticket);
                return res;
            }
            catch (CosmosException)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }
            catch
            {
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}