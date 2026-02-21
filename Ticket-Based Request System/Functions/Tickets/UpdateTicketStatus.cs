using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Ticket_Based_Request_System.Models;
using Ticket_Based_Request_System.Services;

namespace Ticket_Based_Request_System.Functions.Tickets
{
    public class UpdateTicketStatus
    {
        private readonly CosmosDbService _cosmos;

        public UpdateTicketStatus(CosmosDbService cosmos)
        {
            _cosmos = cosmos;
        }

        [Function("UpdateTicketStatus")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "tickets/update-status")]
            HttpRequestData req)
        {
            try
            {
                string role = req.Query["role"];

                if (role != "Admin")
                    return req.CreateResponse(HttpStatusCode.Forbidden);

                string body = await new StreamReader(req.Body).ReadToEndAsync();
                var request = JsonSerializer.Deserialize<UpdateStatusRequest>(body);

                if (request == null || string.IsNullOrWhiteSpace(request.ticketId))
                    return BadRequest(req, "ticketId is required");

                if (string.IsNullOrWhiteSpace(request.status))
                    return BadRequest(req, "status is required");

                string[] allowedStatuses = { "Open", "InProgress", "Resolved", "Closed" };

                if (!allowedStatuses.Contains(request.status))
                    return BadRequest(req, "Invalid status value");

                // 🔎 Same query pattern as BulkUpdateTickets
                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.id = @id")
                    .WithParameter("@id", request.ticketId);

                var iterator = _cosmos.Tickets.GetItemQueryIterator<Ticket>(query);
                var response = await iterator.ReadNextAsync();
                var ticket = response.FirstOrDefault();

                if (ticket == null)
                    return req.CreateResponse(HttpStatusCode.NotFound);

                ticket.status = request.status;
                ticket.updatedAt = DateTime.UtcNow;

                await _cosmos.Tickets.ReplaceItemAsync(
                    ticket,
                    ticket.id,
                    new PartitionKey(ticket.userId));

                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(ticket);
                return res;
            }
            catch
            {
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