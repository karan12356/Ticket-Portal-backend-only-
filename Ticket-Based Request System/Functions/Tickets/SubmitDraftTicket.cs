using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Ticket_Based_Request_System.Models;
using Ticket_Based_Request_System.Services;

namespace Ticket_Based_Request_System.Functions.Tickets
{
    public class SubmitDraftTicket
    {
        private readonly CosmosDbService _cosmos;
        private readonly ILogger<SubmitDraftTicket> _logger;

        public SubmitDraftTicket(CosmosDbService cosmos, ILogger<SubmitDraftTicket> logger)
        {
            _cosmos = cosmos;
            _logger = logger;
        }

        [Function("SubmitDraftTicket")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post",
            Route = "tickets/{userId}/{ticketId}/submit")]
            HttpRequestData req,
            string userId,
            string ticketId)
        {
            _logger.LogInformation(
                "SubmitDraftTicket API triggered. UserId: {UserId}, TicketId: {TicketId}",
                userId, ticketId);

            var container = _cosmos.Tickets;

            Ticket ticket;

            try
            {
                var response = await container.ReadItemAsync<Ticket>(
                    ticketId,
                    new PartitionKey(userId));

                ticket = response.Resource;

                _logger.LogInformation(
                    "Draft ticket retrieved successfully. TicketId: {TicketId}",
                    ticketId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Draft ticket not found. UserId: {UserId}, TicketId: {TicketId}",
                    userId, ticketId);

                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            if (!ticket.isDraft)
            {
                _logger.LogWarning(
                    "Submit draft failed. Ticket is already submitted. TicketId: {TicketId}",
                    ticketId);

                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            var rolePrefix = ticket.role switch
            {
                "Sales Rep" => "W",
                "SVP" => "X",
                "IT Manager" => "Y",
                _ => null
            };

            _logger.LogInformation(
                "Generating confirmation number for TicketId: {TicketId}, Role: {Role}",
                ticketId, ticket.role);

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

            _logger.LogInformation(
                "Draft ticket submitted successfully. TicketId: {TicketId}, ConfirmationNumber: {ConfirmationNumber}",
                ticket.id, ticket.confirmationNumber);

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(ticket);
            return res;
        }
    }
}