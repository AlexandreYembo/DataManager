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

        public CosmosDbGenericRepository(DataSettings settings)
        {
            var db = settings.GetDataBase();

            var dbContextOptionsBuilder = new DbContextOptionsBuilder<DbContext>();
            dbContextOptionsBuilder.UseCosmos(
                accountEndpoint: settings.GetEndpoint(),
                accountKey: settings.GetAuthKey(),
                databaseName: db);

            var context = new DbContext(dbContextOptionsBuilder.Options);
            container = context.Database.GetCosmosClient()
                .GetContainer(db, settings.CurrentEntity);
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

            var response = await container.DeleteItemAsync<JObject>(id, PartitionKey.None, null, CancellationToken.None);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new DbOperationException(response.StatusCode.ToString(), entity.ToString());
            }
        }

        //private PartitionKey GetPartitionKey(Guid id)
        //{
        //    var partitionKey = GetNullablePartitionKey();
        //    if (partitionKey != null)
        //    {
        //        return partitionKey.Value;
        //    }

        //    if (ContainerConfig.PartitionKeyPath?.Equals(IdPartitionKeyPath, StringComparison.OrdinalIgnoreCase) == true)
        //    {
        //        return new PartitionKey(id.ToString());
        //    }

        //    throw new InvalidOperationException("No partition key configured");
        //}

        //private PartitionKey? GetNullablePartitionKey()
        //{
        //    if (!ContainerConfig.IsPartitionedContainer)
        //    {
        //        return PartitionKey.None;
        //    }

        //    if (_partitioningContextFactory.IsInContext)
        //    {
        //        return PartitioningHelper.GetValidPartitionKey(_partitioningContextFactory.CurrentContext
        //            .PartitioningKey);
        //    }

        //    return null;
        //}

    }
}