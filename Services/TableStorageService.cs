using Azure.Data.Tables;
using Azure;

namespace RuneFunctions.Services
{
    public interface ITableStorageService
    {
        Task AddEntityAsync<T>(string tableName, T entity) where T : class, ITableEntity, new();
        Task<List<T>> GetAllEntitiesAsync<T>(string tableName) where T : class, ITableEntity, new();
        Task<List<T>> QueryEntitiesAsync<T>(string tableName, string filter) where T : class, ITableEntity, new();
        Task UpdateEntityAsync<T>(string tableName, T entity) where T : class, ITableEntity, new();
        Task DeleteEntityAsync<T>(string tableName, T entity) where T : class, ITableEntity, new();
    }
    public class TableStorageService : ITableStorageService
    {
        private readonly string? _connectionString = Environment.GetEnvironmentVariable("TableConnectionString");

        private readonly TableServiceClient _tableServiceClient;

        public TableStorageService()
        {
            _tableServiceClient = new TableServiceClient(_connectionString);
        }
        public TableClient GetTableClient(string tableName)
        {
            return new TableClient(_connectionString, tableName);
        }
        

        public async Task AddEntityAsync<T>(string tableName, T entity) where T : class, ITableEntity, new()
        {
            try
            {
                var tableClient = _tableServiceClient.GetTableClient(tableName);
                await tableClient.CreateIfNotExistsAsync();
                await tableClient.UpsertEntityAsync(entity);
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        public async Task<List<T>> GetAllEntitiesAsync<T>(string tableName) where T : class, ITableEntity, new()
        {
            try
            {
                var tableClient = _tableServiceClient.GetTableClient(tableName);
                var entities = new List<T>();

                await foreach (var entity in tableClient.QueryAsync<T>())
                {
                    entities.Add(entity);
                }

                return entities;
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        //method to fetch entities supporting a filter variable
        public async Task<List<T>> QueryEntitiesAsync<T>(string tableName, string filter) where T : class, ITableEntity, new()
        {
            try
            {
                var tableClient = _tableServiceClient.GetTableClient(tableName);
                var entities = new List<T>();
                await foreach (var entity in tableClient.QueryAsync<T>(filter))
                {
                    entities.Add(entity);
                }
                return entities;
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }
        public async Task<T> GetEntityAsync<T>(string tableName, string partitionKey, string rowKey) where T : class, ITableEntity, new()
        {
            try
            {
                var tableClient = _tableServiceClient.GetTableClient(tableName);
                var response = await tableClient.GetEntityAsync<T>(partitionKey, rowKey);
                return response.Value;
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        public async Task UpdateEntityAsync<T>(string tableName, T entity) where T : class, ITableEntity, new()
        {
            try
            {
                var tableClient = _tableServiceClient.GetTableClient(tableName);
                await tableClient.UpsertEntityAsync(entity);
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        public async Task DeleteEntityAsync<T>(string tableName, T entity) where T : class, ITableEntity, new()
        {
            try
            {
                var tableClient = _tableServiceClient.GetTableClient(tableName);
                await tableClient.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, entity.ETag);
            }
            catch (RequestFailedException ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }
    }
}
