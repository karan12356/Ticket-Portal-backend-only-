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
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "tickets/{id}/status")]
            HttpRequestData req,
            string id)
        {
            string userId = req.Query["userId"];

            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest(req, "userId query parameter is required");

            string body = await new StreamReader(req.Body).ReadToEndAsync();
            var data = JsonSerializer.Deserialize<UpdateStatusRequest>(body);

            if (data == null || string.IsNullOrWhiteSpace(data.status))
                return BadRequest(req, "Status is required");

            string[] allowedStatuses = { "Open", "InProgress", "Resolved", "Closed" };

            if (!allowedStatuses.Contains(data.status))
                return BadRequest(req, "Invalid status value");

            try
            {
                var response = await _cosmos.Tickets.ReadItemAsync<Ticket>(
                    id,
                    new PartitionKey(userId));

                var ticket = response.Resource;

                ticket.status = data.status;
                ticket.updatedAt = DateTime.UtcNow;

                await _cosmos.Tickets.ReplaceItemAsync(
                    ticket,
                    ticket.id,
                    new PartitionKey(userId));

                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(ticket);
                return res;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
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

    public class UpdateStatusRequest
    {
        public string status { get; set; }
    }
}
