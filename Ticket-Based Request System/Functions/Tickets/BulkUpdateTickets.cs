using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Ticket_Based_Request_System.Models;
using Ticket_Based_Request_System.Services;

namespace Ticket_Based_Request_System.Functions.Tickets
{
    public class BulkUpdateTickets
    {
        private readonly CosmosDbService _cosmos;

        public BulkUpdateTickets(CosmosDbService cosmos)
        {
            _cosmos = cosmos;
        }

        [Function("BulkUpdateTickets")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "tickets/bulk-update")]
            HttpRequestData req)
        {
            try
            {
                string role = req.Query["role"];


                if (role != "Admin")
                    return req.CreateResponse(HttpStatusCode.Forbidden);

                string body = await new StreamReader(req.Body).ReadToEndAsync();
                var request = JsonSerializer.Deserialize<BulkUpdateRequest>(body);

                if (request == null || request.ticketIds == null || !request.ticketIds.Any())
                    return BadRequest(req, "ticketIds are required");

                if (string.IsNullOrWhiteSpace(request.action))
                    return BadRequest(req, "action is required");

                string[] allowedActions = { "Close", "Assign" };

                if (!allowedActions.Contains(request.action))
                    return BadRequest(req, "Invalid action");

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
                            continue;


                        if (request.action == "Close")
                        {
                            if (ticket.isDraft)
                                continue;

                            ticket.status = "Closed";
                        }


                        if (request.action == "Assign")
                        {
                            if (string.IsNullOrWhiteSpace(request.assignedTo))
                                continue;

                            ticket.assignedTo = request.assignedTo;
                        }

                        ticket.updatedAt = DateTime.UtcNow;

                        await _cosmos.Tickets.ReplaceItemAsync(
                            ticket,
                            ticket.id,
                            new PartitionKey(ticket.userId));

                        updatedTickets.Add(ticket);
                    }
                    catch
                    {

                        continue;
                    }
                }

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
