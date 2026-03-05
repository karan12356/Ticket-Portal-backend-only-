using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Ticket_Based_Request_System.Services;
using Ticket_Based_Request_System.Models;

namespace Ticket_Based_Request_System.Functions.Tickets
{
    public class GetTickets
    {
        private readonly CosmosDbService _cosmos;
        private readonly ILogger<GetTickets> _logger;

        public GetTickets(CosmosDbService cosmos, ILogger<GetTickets> logger)
        {
            _cosmos = cosmos;
            _logger = logger;
        }

        [Function("GetTickets")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tickets")]
            HttpRequestData req)
        {
            try
            {
                _logger.LogInformation("GetTickets API triggered.");

                string role = req.Query["role"];
                string userId = req.Query["userId"];
                int page = int.TryParse(req.Query["page"], out var p) ? p : 1;
                int pageSize = 5;

                _logger.LogInformation(
                    "Ticket fetch request. Role: {Role}, UserId: {UserId}, Page: {Page}",
                    role, userId, page);

                if (string.IsNullOrWhiteSpace(role))
                {
                    _logger.LogWarning("GetTickets failed: Role parameter missing.");
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

                QueryDefinition query;

                if (role == "Admin")
                {
                    _logger.LogInformation("Admin ticket list requested.");

                    query = new QueryDefinition(
                        "SELECT * FROM c ORDER BY c.createdAt DESC");
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(userId))
                    {
                        _logger.LogWarning("GetTickets failed: UserId missing for non-admin role.");
                        return req.CreateResponse(HttpStatusCode.BadRequest);
                    }

                    _logger.LogInformation(
                        "User ticket list requested. UserId: {UserId}", userId);

                    query = new QueryDefinition(
                        "SELECT * FROM c WHERE c.userId = @uid ORDER BY c.createdAt DESC")
                        .WithParameter("@uid", userId);
                }

                var requestOptions = new QueryRequestOptions
                {
                    MaxItemCount = pageSize
                };

                string continuationToken = null;
                FeedResponse<Ticket> response = null;

                for (int i = 1; i <= page; i++)
                {
                    var iterator = _cosmos.Tickets.GetItemQueryIterator<Ticket>(
                        query,
                        continuationToken,
                        requestOptions);

                    if (!iterator.HasMoreResults)
                        break;

                    response = await iterator.ReadNextAsync();
                    continuationToken = response.ContinuationToken;
                }

                var ticketsList = response?.Resource?.ToList() ?? new List<Ticket>();

                _logger.LogInformation(
                    "Tickets retrieved. Count: {Count}, Page: {Page}",
                    ticketsList.Count, page);

                foreach (var ticket in ticketsList)
                {
                    if (ticket.isConfidential)
                    {
                        ticket.description = "Confidential – Requires Admin Password to View";
                    }
                }

                var result = new
                {
                    page,
                    pageSize,
                    tickets = ticketsList,
                    nextPageToken = continuationToken
                };

                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(result);

                _logger.LogInformation(
                    "GetTickets response sent. Page: {Page}, TicketsReturned: {Count}",
                    page, ticketsList.Count);

                return res;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching tickets.");

                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteStringAsync(ex.Message);
                return err;
            }
        }
    }
}