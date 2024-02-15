using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Migration.Core;
using Migration.Core.Exceptions;
using Migration.Models;
using Newtonsoft.Json.Linq;
using System.Net;

namespace Connectors.Azure.CosmosDb.Repository
{
    public class CosmosDbGenericRepository : IGenericRepository
    {
        private readonly Container container;
        private readonly DataSettings _settings;

        public CosmosDbGenericRepository(DataSettings settings)
        {
            if (string.IsNullOrEmpty(settings.CurrentEntity.Name))
                return;

            _settings = settings;

            var db = settings.GetDataBase();

            var dbContextOptionsBuilder = new DbContextOptionsBuilder<DbContext>();
            dbContextOptionsBuilder.UseCosmos(
                accountEndpoint: settings.GetEndpoint(),
                accountKey: settings.GetAuthKey(),
                databaseName: db);

            var context = new DbContext(dbContextOptionsBuilder.Options);

            var client = context.Database.GetCosmosClient();
            container = client.GetContainer(db, settings.CurrentEntity.Name);
        }

        public async Task CreateTableAsync()
        {
            var partitionKey = _settings.CurrentEntity.Attributes.FirstOrDefault(w => w.Key == "PartitionKey")?.Value?.Replace("/", string.Empty);

            var throughput = ThroughputProperties.CreateManualThroughput(400);

            var containerResponse = await container.Database.CreateContainerIfNotExistsAsync(
                new ContainerProperties()
                {
                    Id = _settings.CurrentEntity.Name,
                    PartitionKeyPath = $"/{partitionKey}"
                }, throughput);

            if (containerResponse.StatusCode != HttpStatusCode.Created && containerResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new TableOperationException(DbOperationErrorCodeConstants.ERROR_CREATE_TABLE, $"Error to create {_settings.CurrentEntity}");
            }
        }

        public async Task InsertAsync(RepositoryParameters parameters)
        {
            var entity = parameters.Data;

            ItemResponse<JObject> response;
            var hasId = entity.SelectToken("id") != null;

            if (!hasId)
            {
                entity["id"] = Guid.NewGuid().ToString();
            }

            Guid.TryParse(entity["id"].ToString(), out var id);

            if (id == default)
            {
                entity["id"] = Guid.NewGuid().ToString();
            }

            try
            {
                var partitionKey = _settings.CurrentEntity.Attributes.FirstOrDefault(w => w.Key == Constants.PARTITION_KEY)?.Value?.Replace("/", string.Empty);

                if (!string.IsNullOrEmpty(partitionKey))
                {
                    var partitionIdValue = string.Empty;

                    if (entity[partitionKey] == null)
                    {
                        partitionIdValue = id.ToString();
                    }
                    else
                    {
                        partitionIdValue = entity[partitionKey].ToString();
                    }

                    response = await container.CreateItemAsync(entity, new PartitionKey(partitionIdValue));
                }
                else
                {
                    response = await container.CreateItemAsync(entity, PartitionKey.None);
                }

                if (response.StatusCode != HttpStatusCode.Created)
                {
                    throw new TableOperationException(DbOperationErrorCodeConstants.ERROR_INSERT_OPERATION, entity.ToString());
                }
            }
            catch (Exception e)
            {
                throw new TableOperationException(DbOperationErrorCodeConstants.GENERIC_EXCEPTION, e.Message);
            }
        }

        public async Task UpdateAsync(RepositoryParameters parameters)
        {
            var entity = parameters.Data;
          
            var response = await container.UpsertItemAsync(entity);
            
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new TableOperationException(DbOperationErrorCodeConstants.ERROR_UPDATE_OPERATION, entity.ToString());
            }
        }

        public async Task DeleteAsync(RepositoryParameters parameters)
        {
            var entity = parameters.Data;
          
            var id = entity["id"].ToString();
            ItemResponse<JObject> response;

            var partitionKey = _settings.CurrentEntity.Attributes.FirstOrDefault(w => w.Key == "PartitionKey")?.Value?.Replace("/", string.Empty);

            if (!string.IsNullOrEmpty(partitionKey))
            {
                var partitionKeyValue = entity[partitionKey].ToString();

                response = await container.DeleteItemAsync<JObject>(id, new PartitionKey(partitionKeyValue), null, CancellationToken.None);
            }
            else
            {
                response = await container.DeleteItemAsync<JObject>(id, PartitionKey.None, null, CancellationToken.None);
            }

            if (response.StatusCode != HttpStatusCode.NoContent)
            {
                throw new TableOperationException(DbOperationErrorCodeConstants.ERROR_DELETE_OPERATION, entity.ToString());
            }
        }

        public async Task<Dictionary<string, JObject>> GetAsync(string rawQuery)
        {
            var query = QueryBuilder.Build(rawQuery);

            Dictionary<string, JObject> dictionary = new();

            using FeedIterator<dynamic> feedIterator = container.GetItemQueryIterator<dynamic>(query);

            var nextResult = true;
            while (nextResult)
            {
                var responseRecord = await feedIterator.ReadNextAsync();

                nextResult = responseRecord.Count > 0;

                if (nextResult)
                {
                    foreach (var record in responseRecord.ToList())
                    {
                        (record as JObject).Remove("_ts");
                        (record as JObject).Remove("_etag");
                        (record as JObject).Remove("_rid");
                        (record as JObject).Remove("_self");
                        (record as JObject).Remove("_attachments");

                        JToken jToken = JToken.FromObject(record);

                        var value = ((JValue)jToken["id"]).Value;
                        dictionary[value.ToString()] = JObject.Parse(jToken.ToString());
                    }
                }
            }

            return dictionary;
        }

        public async Task<Dictionary<string, JObject>> GetAsync(RepositoryParameters parameters)
        {
            var data = parameters.Data;
            var fieldMappings = parameters.FieldMappings;
            var take = parameters.Pagination.Take;
            var skip = parameters.Pagination.Skip;
            var rawQuery = parameters.Query;

            var query = QueryBuilder.Build(rawQuery, fieldMappings, data, take, skip);

            Dictionary<string, JObject> dictionary = new();

            if (string.IsNullOrEmpty(query)) return dictionary;

            using FeedIterator<dynamic> feedIterator = container.GetItemQueryIterator<dynamic>(query);

            var nextResult = true;
            while (nextResult)
            {
                try
                {
                    var responseRecord = await feedIterator.ReadNextAsync();

                    nextResult = responseRecord.Count > 0;

                    if (nextResult)
                    {
                        foreach (var record in responseRecord.ToList())
                        {
                            (record as JObject).Remove("_ts");
                            (record as JObject).Remove("_etag");
                            (record as JObject).Remove("_rid");
                            (record as JObject).Remove("_self");
                            (record as JObject).Remove("_attachments");

                            JToken jToken = JToken.FromObject(record);

                            if (rawQuery.Contains("distinct", StringComparison.CurrentCultureIgnoreCase))
                            {
                                dictionary[Guid.NewGuid().ToString()] = JObject.Parse(jToken.ToString());
                            }
                            else
                            {
                                var value = ((JValue)jToken["id"]).Value;
                                dictionary[value.ToString()] = JObject.Parse(jToken.ToString());
                            }
                        }
                    }
                }
                catch
                {
                    nextResult = false;
                    throw;
                }
            }

            return dictionary;
        }

        public async Task<DataSettings> TestConnection()
        {
            _settings.Entities = new();
            using CosmosClient client = new(_settings.GetEndpoint(), _settings.GetAuthKey());
            var database = client.GetDatabase(_settings.GetDataBase());

            FeedIterator<ContainerProperties> iterator = database.GetContainerQueryIterator<ContainerProperties>();
            FeedResponse<ContainerProperties> containers = await iterator.ReadNextAsync().ConfigureAwait(false);

            foreach (var container in containers)
            {
                // do what you want with the container
                _settings.Entities.Add(new Entity(container.Id)
                {
                    Attributes = new()
                    {
                        new()
                        {
                            Key = "PartitionKey",
                            Value = container.PartitionKeyPath
                        }
                    }
                });
            }

            return _settings;
        }
    }
}