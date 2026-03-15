using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Ticket_Based_Request_System.Models;
using Ticket_Based_Request_System.Services;

namespace Ticket_Based_Request_System.Functions.Tickets
{
    public class UpdateTicketStatus
    {
        private readonly CosmosDbService _cosmos;
        private readonly ILogger<UpdateTicketStatus> _logger;

        public UpdateTicketStatus(CosmosDbService cosmos, ILogger<UpdateTicketStatus> logger)
        {
            _cosmos = cosmos;
            _logger = logger;
        }

        [Function("UpdateTicketStatus")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "tickets/update-status")]
            HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("UpdateTicketStatus API triggered.");

                string role = req.Query["role"];

                if (role != "Admin")
                {
                    _logger.LogWarning("Unauthorized status update attempt. Role: {Role}", role);
                    return req.CreateResponse(HttpStatusCode.Forbidden);
                }

                string body = await new StreamReader(req.Body).ReadToEndAsync();
                var request = JsonSerializer.Deserialize<UpdateStatusRequest>(body);

                if (request == null || string.IsNullOrWhiteSpace(request.ticketId))
                {
                    _logger.LogWarning("Status update failed. ticketId missing.");
                    return BadRequest(req, "ticketId is required");
                }

                if (string.IsNullOrWhiteSpace(request.status))
                {
                    _logger.LogWarning("Status update failed. Status missing for TicketId: {TicketId}", request.ticketId);
                    return BadRequest(req, "status is required");
                }

                string[] allowedStatuses = { "Open", "InProgress", "Resolved", "Closed" };

                if (!allowedStatuses.Contains(request.status))
                {
                    _logger.LogWarning(
                        "Invalid status update attempted. TicketId: {TicketId}, Status: {Status}",
                        request.ticketId, request.status);

                    return BadRequest(req, "Invalid status value");
                }

                _logger.LogInformation(
                    "Admin requested ticket status update. TicketId: {TicketId}, NewStatus: {Status}",
                    request.ticketId, request.status);

                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.id = @id")
                    .WithParameter("@id", request.ticketId);

                var iterator = _cosmos.Tickets.GetItemQueryIterator<Ticket>(query);
                var response = await iterator.ReadNextAsync();
                var ticket = response.FirstOrDefault();

                if (ticket == null)
                {
                    _logger.LogWarning(
                        "Ticket not found for status update. TicketId: {TicketId}",
                        request.ticketId);

                    return req.CreateResponse(HttpStatusCode.NotFound);
                }

                ticket.status = request.status;
                ticket.updatedAt = DateTime.UtcNow;

                if (ticket.ticketHistory == null)
                    ticket.ticketHistory = new List<TicketHistory>();

                ticket.ticketHistory.Add(new TicketHistory
                {
                    action = $"Status changed to {request.status}",
                    performedBy = "Admin",
                    timestamp = DateTime.UtcNow
                });

                await _cosmos.Tickets.ReplaceItemAsync(
                    ticket,
                    ticket.id,
                    new PartitionKey(ticket.userId));

                _logger.LogInformation(
                    "Ticket status updated successfully. TicketId: {TicketId}, Status: {Status}",
                    ticket.id, ticket.status);

                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(ticket);
                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating ticket status.");

                return Error(req, HttpStatusCode.BadGateway, "Failed to update ticket status");
            }
        }

        private HttpResponseData BadRequest(HttpRequestData req, string msg)
        {
            var res = req.CreateResponse(HttpStatusCode.BadRequest);
            res.WriteString(msg);
            return res;
        }

        private HttpResponseData Error(HttpRequestData req, HttpStatusCode code, string msg)
        {
            var res = req.CreateResponse(code);
            res.WriteString(msg);
            return res;
        }
    }
}