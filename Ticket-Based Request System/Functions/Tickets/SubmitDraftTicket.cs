using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Cosmos;
using Ticket_Based_Request_System.Models;
using Ticket_Based_Request_System.Services;

namespace Ticket_Based_Request_System.Functions.Tickets
{
    public class SubmitDraftTicket
    {
        private readonly CosmosDbService _cosmos;

        public SubmitDraftTicket(CosmosDbService cosmos)
        {
            _cosmos = cosmos;
        }

        [Function("SubmitDraftTicket")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post",
            Route = "tickets/{userId}/{ticketId}/submit")]
            HttpRequestData req,
            string userId,
            string ticketId)
        {
            var container = _cosmos.Tickets;

            Ticket ticket;

            try
            {
                var response = await container.ReadItemAsync<Ticket>(
                    ticketId,
                    new PartitionKey(userId));

                ticket = response.Resource;
            }
            catch
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            if (!ticket.isDraft)
                return req.CreateResponse(HttpStatusCode.BadRequest);

            var rolePrefix = ticket.role switch
            {
                "Sales Rep" => "W",
                "SVP" => "X",
                "IT Manager" => "Y",
                _ => null
            };

            var counterResponse = await _cosmos.Counters.ReadItemAsync<dynamic>(
                rolePrefix,
                new PartitionKey("ticket"));

            int nextNumber = counterResponse.Resource.currentValue + 1;
            counterResponse.Resource.currentValue = nextNumber;

            await _cosmos.Counters.ReplaceItemAsync(
                counterResponse.Resource,
                rolePrefix,
                new PartitionKey("ticket"));

            ticket.confirmationNumber = $"{rolePrefix}-{nextNumber:D5}";
            ticket.isDraft = false;
            ticket.status = "Open";
            ticket.submittedAt = DateTime.UtcNow;
            ticket.updatedAt = DateTime.UtcNow;

            await container.ReplaceItemAsync(
                ticket,
                ticket.id,
                new PartitionKey(userId));

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(ticket);
            return res;
        }
    }
}
