using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Ticket_Based_Request_System.Repositories;

namespace Ticket_Based_Request_System.Services
{
    public class CosmosDbService
    {
        private readonly CosmosClient _client;
        private readonly Database _database;
        private readonly CosmosRepository _repository;

        public CosmosDbService(IConfiguration configuration)
        {
            _client = new CosmosClient(
                configuration["CosmosDb:Endpoint"],
                configuration["CosmosDb:Key"]);

            _database = _client.GetDatabase(
                configuration["CosmosDb:DatabaseName"]);

            _repository = new CosmosRepository(this);
        }

        public Container Users =>
            _database.GetContainer("Users");

        public Container Tickets =>
            _database.GetContainer("Tickets");

        public Container Counters =>
            _database.GetContainer("Counters");

        // SERVICE METHODS

        public async Task CreateAsync<T>(string container, T item, string partitionKey)
        {
            await _repository.CreateItemAsync(container, item, partitionKey);
        }

        public async Task<T?> GetAsync<T>(string container, string id, string partitionKey)
        {
            return await _repository.GetItemAsync<T>(container, id, partitionKey);
        }

        public async Task<(List<T>, string?)> QueryAsync<T>(
            string container,
            QueryDefinition query,
            int pageSize,
            string? continuationToken)
        {
            return await _repository.QueryItemsAsync<T>(
                container,
                query,
                pageSize,
                continuationToken);
        }

        public async Task UpsertAsync<T>(string container, T item, string partitionKey)
        {
            await _repository.UpsertItemAsync(container, item, partitionKey);
        }
    }
}