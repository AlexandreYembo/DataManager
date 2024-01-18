using System.Dynamic;
using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
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
            _settings = settings;

            var db = settings.GetDataBase();

            var dbContextOptionsBuilder = new DbContextOptionsBuilder<DbContext>();
            dbContextOptionsBuilder.UseCosmos(
                accountEndpoint: settings.GetEndpoint(),
                accountKey: settings.GetAuthKey(),
                databaseName: db);
           
            var context = new DbContext(dbContextOptionsBuilder.Options);

            var client = context.Database.GetCosmosClient();
            container = client.GetContainer(db, settings.CurrentEntity);
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

            var partitionKey = _settings.GetPartitionKey();

            if (!string.IsNullOrEmpty(partitionKey))
            {
                response = await container.DeleteItemAsync<JObject>(id, new PartitionKey(partitionKey), null, CancellationToken.None);
            }
            else
            {
                response = await container.DeleteItemAsync<JObject>(id, PartitionKey.None, null, CancellationToken.None);
            }
          
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new DbOperationException(response.StatusCode.ToString(), entity.ToString());
            }
        }

        public async Task Insert(JObject entity)
        {
            ItemResponse<JObject> response;

            var partitionKey = _settings.GetPartitionKey();
            var idFromData = _settings.GetIdentityKey();

            entity["id"] = entity.SelectToken(idFromData);

            Guid.TryParse(entity["id"].ToString(), out var id);

            if (id == default)
            {
                //Subscribe log with the error
            }

            try
            {
                var throughput = ThroughputProperties.CreateManualThroughput(400);

                var containerResponse = await container.Database.CreateContainerIfNotExistsAsync(new ContainerProperties()
                {
                    Id = _settings.CurrentEntity,
                    PartitionKeyPath = $"/{partitionKey}"
                }, throughput);

                if (containerResponse.StatusCode != HttpStatusCode.Created && containerResponse.StatusCode != HttpStatusCode.OK)
                {
                    throw new DbOperationException(containerResponse.StatusCode.ToString(), $"Error to create {_settings.CurrentEntity}");
                }

                if (!string.IsNullOrEmpty(partitionKey))
                {
                    response = await container.CreateItemAsync(entity, new PartitionKey(entity[partitionKey].ToString()));
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
                Console.WriteLine(e);
                throw;
            }
         
        }
    }
}