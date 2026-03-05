using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Ticket_Based_Request_System.Models;
using Ticket_Based_Request_System.Services;
using Ticket_Based_Request_System.Helpers;

namespace Ticket_Based_Request_System.Functions.Tickets
{
    public class UpdateTicket
    {
        private readonly CosmosDbService _cosmos;
        private readonly ILogger<UpdateTicket> _logger;

        public UpdateTicket(CosmosDbService cosmos, ILogger<UpdateTicket> logger)
        {
            _cosmos = cosmos;
            _logger = logger;
        }

        [Function("UpdateTicket")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "put",
            Route = "tickets/{userId}/{ticketId}")]
            HttpRequestData req,
            string userId,
            string ticketId)
        {
            _logger.LogInformation(
                "UpdateTicket API triggered. TicketId: {TicketId}, UserId: {UserId}",
                ticketId, userId);

            var container = _cosmos.Tickets;

            Ticket ticket;

            try
            {
                var response = await container.ReadItemAsync<Ticket>(
                    ticketId,
                    new PartitionKey(userId));

                ticket = response.Resource;

                _logger.LogInformation(
                    "Ticket fetched successfully for update. TicketId: {TicketId}",
                    ticketId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Ticket not found for update. TicketId: {TicketId}, UserId: {UserId}",
                    ticketId, userId);

                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            if (!ticket.isDraft)
            {
                _logger.LogWarning(
                    "Update rejected. Only draft tickets can be updated. TicketId: {TicketId}",
                    ticketId);

                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            var body = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body);

            bool isConfidentialUpdated = false;
            bool newConfidentialValue = ticket.isConfidential;

            if (body.TryGetProperty("title", out var title) &&
                title.ValueKind != JsonValueKind.Null)
            {
                ticket.title = title.GetString();
                _logger.LogInformation("Ticket title updated. TicketId: {TicketId}", ticketId);
            }

            if (body.TryGetProperty("category", out var cat) &&
                cat.ValueKind != JsonValueKind.Null)
            {
                ticket.category = cat.GetString();
                _logger.LogInformation("Ticket category updated. TicketId: {TicketId}", ticketId);
            }

            if (body.TryGetProperty("status", out var status) &&
                status.ValueKind != JsonValueKind.Null)
            {
                ticket.status = status.GetString();
                _logger.LogInformation("Ticket status updated. TicketId: {TicketId}", ticketId);
            }

            if (body.TryGetProperty("isConfidential", out var confidential) &&
                confidential.ValueKind != JsonValueKind.Null)
            {
                newConfidentialValue = confidential.GetBoolean();
                isConfidentialUpdated = true;

                _logger.LogInformation(
                    "Confidential flag update requested. TicketId: {TicketId}, NewValue: {Value}",
                    ticketId, newConfidentialValue);
            }

            if (body.TryGetProperty("description", out var desc) &&
                desc.ValueKind != JsonValueKind.Null)
            {
                var newDescription = desc.GetString();

                if (newConfidentialValue)
                {
                    ticket.description = EncryptionHelper.Encrypt(newDescription);
                    _logger.LogInformation(
                        "Encrypted description stored for confidential ticket. TicketId: {TicketId}",
                        ticketId);
                }
                else
                {
                    ticket.description = newDescription;
                    _logger.LogInformation(
                        "Description updated for ticket. TicketId: {TicketId}",
                        ticketId);
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

            _logger.LogInformation(
                "Ticket updated successfully. TicketId: {TicketId}",
                ticketId);

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(ticket);
            return res;
        }
    }
}