using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace Ticket_Based_Request_System.Services
{
    public class CosmosDbService
    {
        private readonly CosmosClient _client;
        private readonly Database _database;

        public CosmosDbService(IConfiguration configuration)
        {
            _client = new CosmosClient(
                configuration["CosmosDb:Endpoint"],
                configuration["CosmosDb:Key"]
            );

            _database = _client.GetDatabase(
                configuration["CosmosDb:DatabaseName"]
            );
        }

        public Microsoft.Azure.Cosmos.Container Users =>
            _database.GetContainer("Users");

        public Microsoft.Azure.Cosmos.Container Tickets =>
            _database.GetContainer("Tickets");

        public Microsoft.Azure.Cosmos.Container Counters =>
            _database.GetContainer("Counters");
    }
}
