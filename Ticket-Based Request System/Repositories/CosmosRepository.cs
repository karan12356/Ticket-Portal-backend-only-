using Microsoft.Azure.Cosmos;
using Ticket_Based_Request_System.Services;

namespace Ticket_Based_Request_System.Repositories
{
    public class CosmosRepository
    {
        private readonly CosmosDbService _cosmos;

        public CosmosRepository(CosmosDbService cosmos)
        {
            _cosmos = cosmos;
        }

        // CREATE
        public async Task CreateItemAsync<T>(string containerName, T item, string partitionKey)
        {
            var container = GetContainer(containerName);

            await container.CreateItemAsync(
                item,
                new PartitionKey(partitionKey));
        }

        // READ BY ID
        public async Task<T?> GetItemAsync<T>(
            string containerName,
            string id,
            string partitionKey)
        {
            var container = GetContainer(containerName);

            try
            {
                var response = await container.ReadItemAsync<T>(
                    id,
                    new PartitionKey(partitionKey));

                return response.Resource;
            }
            catch
            {
                return default;
            }
        }

        // QUERY
        public async Task<(List<T>, string?)> QueryItemsAsync<T>(
            string containerName,
            QueryDefinition query,
            int pageSize,
            string? continuationToken)
        {
            var container = GetContainer(containerName);

            var iterator = container.GetItemQueryIterator<T>(
                query,
                continuationToken,
                new QueryRequestOptions
                {
                    MaxItemCount = pageSize
                });

            var response = await iterator.ReadNextAsync();

            return (response.ToList(), response.ContinuationToken);
        }

        // UPDATE / UPSERT
        public async Task UpsertItemAsync<T>(
            string containerName,
            T item,
            string partitionKey)
        {
            var container = GetContainer(containerName);

            await container.UpsertItemAsync(
                item,
                new PartitionKey(partitionKey));
        }

        private Container GetContainer(string name)
        {
            return name switch
            {
                "Users" => _cosmos.Users,
                "Tickets" => _cosmos.Tickets,
                "Counters" => _cosmos.Counters,
                _ => throw new Exception("Invalid container")
            };
        }
    }
}