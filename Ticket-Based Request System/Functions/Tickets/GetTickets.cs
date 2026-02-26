using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Cosmos;
using Ticket_Based_Request_System.Services;
using Ticket_Based_Request_System.Models;

namespace Ticket_Based_Request_System.Functions.Tickets
{
    public class GetTickets
    {
        private readonly CosmosDbService _cosmos;

        public GetTickets(CosmosDbService cosmos)
        {
            _cosmos = cosmos;
        }

        [Function("GetTickets")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tickets")]
            HttpRequestData req)
        {
            try
            {
                string role = req.Query["role"];
                string userId = req.Query["userId"];
                int page = int.TryParse(req.Query["page"], out var p) ? p : 1;
                int pageSize = 5;

                if (string.IsNullOrWhiteSpace(role))
                    return req.CreateResponse(HttpStatusCode.BadRequest);

                QueryDefinition query;

                if (role == "Admin")
                {
                    query = new QueryDefinition(
                        "SELECT * FROM c ORDER BY c.createdAt DESC");
                }
               
                else
                {
                    if (string.IsNullOrWhiteSpace(userId))
                        return req.CreateResponse(HttpStatusCode.BadRequest);

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
                return res;
            }
            catch (Exception ex)
            {
                var err = req.CreateResponse(HttpStatusCode.InternalServerError);
                await err.WriteStringAsync(ex.Message);
                return err;
            }
        }
    }
}