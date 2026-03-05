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
    public class BulkUpdateTickets
    {
        private readonly CosmosDbService _cosmos;
        private readonly ILogger<BulkUpdateTickets> _logger;

        public BulkUpdateTickets(CosmosDbService cosmos, ILogger<BulkUpdateTickets> logger)
        {
            _cosmos = cosmos;
            _logger = logger;
        }

        [Function("BulkUpdateTickets")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "tickets/bulk-update")]
            HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("BulkUpdateTickets API triggered.");

                string role = req.Query["role"];

                if (role != "Admin")
                {
                    _logger.LogWarning("Unauthorized bulk update attempt. Role: {Role}", role);
                    return req.CreateResponse(HttpStatusCode.Forbidden);
                }

                string body = await new StreamReader(req.Body).ReadToEndAsync();
                var request = JsonSerializer.Deserialize<BulkUpdateRequest>(body);

                if (request == null || request.ticketIds == null || !request.ticketIds.Any())
                {
                    _logger.LogWarning("Bulk update failed. ticketIds missing.");
                    return BadRequest(req, "ticketIds are required");
                }

                if (string.IsNullOrWhiteSpace(request.action))
                {
                    _logger.LogWarning("Bulk update failed. action missing.");
                    return BadRequest(req, "action is required");
                }

                string[] allowedActions = { "Close", "Assign" };

                if (!allowedActions.Contains(request.action))
                {
                    _logger.LogWarning(
                        "Invalid bulk action attempted. Action: {Action}",
                        request.action);

                    return BadRequest(req, "Invalid action");
                }

                _logger.LogInformation(
                    "Admin bulk action started. Action: {Action}, TicketCount: {Count}",
                    request.action, request.ticketIds.Count);

                var updatedTickets = new List<Ticket>();

                foreach (var ticketId in request.ticketIds)
                {
                    try
                    {
                        var query = new QueryDefinition(
                            "SELECT * FROM c WHERE c.id = @id")
                            .WithParameter("@id", ticketId);

                        var iterator = _cosmos.Tickets.GetItemQueryIterator<Ticket>(query);
                        var response = await iterator.ReadNextAsync();
                        var ticket = response.FirstOrDefault();

                        if (ticket == null)
                        {
                            _logger.LogWarning(
                                "Ticket not found during bulk update. TicketId: {TicketId}",
                                ticketId);
                            continue;
                        }

                        if (request.action == "Close")
                        {
                            if (ticket.isDraft)
                            {
                                _logger.LogWarning(
                                    "Draft ticket skipped during bulk close. TicketId: {TicketId}",
                                    ticketId);
                                continue;
                            }

                            ticket.status = "Closed";

                            _logger.LogInformation(
                                "Ticket closed via bulk update. TicketId: {TicketId}",
                                ticketId);
                        }

                        if (request.action == "Assign")
                        {
                            if (string.IsNullOrWhiteSpace(request.assignedTo))
                            {
                                _logger.LogWarning(
                                    "Assign action skipped. assignedTo missing for TicketId: {TicketId}",
                                    ticketId);
                                continue;
                            }

                            ticket.assignedTo = request.assignedTo;

                            _logger.LogInformation(
                                "Ticket assigned via bulk update. TicketId: {TicketId}, AssignedTo: {AssignedTo}",
                                ticketId, request.assignedTo);
                        }

                        ticket.updatedAt = DateTime.UtcNow;

                        await _cosmos.Tickets.ReplaceItemAsync(
                            ticket,
                            ticket.id,
                            new PartitionKey(ticket.userId));

                        updatedTickets.Add(ticket);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Error updating ticket during bulk operation. TicketId: {TicketId}",
                            ticketId);

                        continue;
                    }
                }

                _logger.LogInformation(
                    "Bulk update completed. UpdatedCount: {Count}",
                    updatedTickets.Count);

                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(new
                {
                    updatedCount = updatedTickets.Count,
                    tickets = updatedTickets
                });

                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bulk ticket update failed.");

                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteStringAsync(ex.Message);
                return err;
            }
        }

        private HttpResponseData BadRequest(HttpRequestData req, string msg)
        {
            var res = req.CreateResponse(HttpStatusCode.BadRequest);
            res.WriteString(msg);
            return res;
        }
    }
}