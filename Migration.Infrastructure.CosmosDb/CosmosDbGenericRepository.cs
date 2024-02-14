using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Migration.Repository;
using Migration.Repository.Exceptions;
using Migration.Repository.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Infrastructure.CosmosDb
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

        public async Task<Dictionary<string, string>> Get(string rawQuery)
        {
            var query = QueryBuilder.Build(rawQuery);

            Dictionary<string, string> dictionary = new();

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
                        dictionary[value.ToString()] = jToken.ToString();
                    }
                }
            }

            return dictionary;
        }

        public async Task<Dictionary<string, string>> Get(string rawQuery, List<DataFieldsMapping> fieldMappings, string data, int take, int skip = 0)
        {
            var query = QueryBuilder.Build(rawQuery, fieldMappings, data, take, skip);

            Dictionary<string, string> dictionary = new();

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
                                dictionary[Guid.NewGuid().ToString()] = jToken.ToString();
                            }
                            else
                            {
                                var value = ((JValue)jToken["id"]).Value;
                                dictionary[value.ToString()] = jToken.ToString();
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

        public async Task Update(JObject entity, List<DataFieldsMapping> fieldMappings = null)
        {
            var response = await container.UpsertItemAsync(entity);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new DbOperationException(response.StatusCode.ToString(), entity.ToString());
            }
        }

        public async Task Delete(JObject entity)
        {
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
                throw new DbOperationException(response.StatusCode.ToString(), entity.ToString());
            }
        }

        public async Task Insert(JObject entity, List<DataFieldsMapping> fieldMappings = null)
        {
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
                var partitionKey = _settings.CurrentEntity.Attributes.FirstOrDefault(w => w.Key == "PartitionKey")?.Value?.Replace("/", string.Empty);

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
                    throw new DbOperationException(response.StatusCode.ToString(), entity.ToString());
                }
            }
            catch (Exception e)
            {
                throw new DbOperationException("DB-9999", e.Message);
            }
        }

        public async Task CreateTable()
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
                throw new DbOperationException(containerResponse.StatusCode.ToString(), $"Error to create {_settings.CurrentEntity}");
            }
        }
    }
}