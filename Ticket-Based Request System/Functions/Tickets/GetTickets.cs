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
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "tickets")]
            HttpRequestData req)
        {
            try
            {
                string userId = req.Query["userId"];
                int page = int.TryParse(req.Query["page"], out var p) ? p : 1;
                int pageSize = 5;

                if (string.IsNullOrEmpty(userId))
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

                var query = new QueryDefinition(
                    "SELECT * FROM c WHERE c.userId = @uid ORDER BY c.createdAt DESC")
                    .WithParameter("@uid", userId);

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

                    response = await iterator.ReadNextAsync();
                    continuationToken = response.ContinuationToken;
                }

                var result = new
                {
                    page,
                    pageSize,
                    tickets = response.Resource,
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
