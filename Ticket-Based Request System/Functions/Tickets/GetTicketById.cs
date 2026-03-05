using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<GetTicketById> _logger;

        public GetTicketById(CosmosDbService cosmos, ILogger<GetTicketById> logger)
        {
            _cosmos = cosmos;
            _logger = logger;
        }

        [Function("GetTicketById")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "tickets/{id}")]
            HttpRequestData req,
            string id)
        {
            string userId = req.Query["userId"];
            string role = req.Query["role"];
            string adminPass = req.Query["adminPass"];

            _logger.LogInformation(
                "GetTicketById API triggered. TicketId: {TicketId}, UserId: {UserId}, Role: {Role}",
                id, userId, role);

            try
            {
                var response = await _cosmos.Tickets.ReadItemAsync<Ticket>(
                    id,
                    new PartitionKey(userId));

                var ticket = response.Resource;

                _logger.LogInformation(
                    "Ticket retrieved successfully. TicketId: {TicketId}, OwnerUserId: {UserId}",
                    id, ticket.userId);

                if (ticket.isConfidential)
                {
                    _logger.LogInformation(
                        "Confidential ticket access attempted. TicketId: {TicketId}, Role: {Role}",
                        id, role);

                    if (role != "Admin")
                    {
                        _logger.LogWarning(
                            "Unauthorized access attempt to confidential ticket. TicketId: {TicketId}, Role: {Role}",
                            id, role);

                        var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                        await unauthorized.WriteStringAsync("Only Admin can view confidential tickets.");
                        return unauthorized;
                    }

                    if (string.IsNullOrWhiteSpace(adminPass))
                    {
                        _logger.LogWarning(
                            "Admin password missing for confidential ticket access. TicketId: {TicketId}",
                            id);

                        var badReq = req.CreateResponse(HttpStatusCode.BadRequest);
                        await badReq.WriteStringAsync("Admin pass required.");
                        return badReq;
                    }

                    if (!AdminTicketAccessHelper.Validate(ticket, adminPass))
                    {
                        _logger.LogWarning(
                            "Invalid admin password attempt for confidential ticket. TicketId: {TicketId}",
                            id);

                        return req.CreateResponse(HttpStatusCode.Unauthorized);
                    }

                    _logger.LogInformation(
                        "Admin authorized to view confidential ticket. TicketId: {TicketId}",
                        id);

                    ticket.description = EncryptionHelper.Decrypt(ticket.description);
                }

                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(ticket);

                _logger.LogInformation(
                    "Ticket response sent successfully. TicketId: {TicketId}",
                    id);

                return res;
            }
            catch (CosmosException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Ticket not found. TicketId: {TicketId}, UserId: {UserId}",
                    id, userId);

                return req.CreateResponse(HttpStatusCode.NotFound);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error occurred while fetching ticket. TicketId: {TicketId}",
                    id);

                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}