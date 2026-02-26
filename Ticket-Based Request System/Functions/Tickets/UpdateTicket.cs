using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Cosmos;
using Ticket_Based_Request_System.Models;
using Ticket_Based_Request_System.Services;
using Ticket_Based_Request_System.Helpers;

namespace Ticket_Based_Request_System.Functions.Tickets
{
    public class UpdateTicket
    {
        private readonly CosmosDbService _cosmos;

        public UpdateTicket(CosmosDbService cosmos)
        {
            _cosmos = cosmos;
        }

        [Function("UpdateTicket")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "put",
            Route = "tickets/{userId}/{ticketId}")]
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

            var body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body);

            bool isConfidentialUpdated = false;
            bool newConfidentialValue = ticket.isConfidential;

            if (body.TryGetProperty("title", out var title) &&
                title.ValueKind != JsonValueKind.Null)
                ticket.title = title.GetString();

            if (body.TryGetProperty("category", out var cat) &&
                cat.ValueKind != JsonValueKind.Null)
                ticket.category = cat.GetString();

            if (body.TryGetProperty("status", out var status) &&
                status.ValueKind != JsonValueKind.Null)
                ticket.status = status.GetString();

            if (body.TryGetProperty("isConfidential", out var confidential) &&
                confidential.ValueKind != JsonValueKind.Null)
            {
                newConfidentialValue = confidential.GetBoolean();
                isConfidentialUpdated = true;
            }

            if (body.TryGetProperty("description", out var desc) &&
                desc.ValueKind != JsonValueKind.Null)
            {
                var newDescription = desc.GetString();

               
                if (newConfidentialValue)
                {
                    ticket.description = EncryptionHelper.Encrypt(newDescription);
                }
                else
                {
                    ticket.description = newDescription;
                }
            }

            
            if (isConfidentialUpdated)
            {
                ticket.isConfidential = newConfidentialValue;
            }

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