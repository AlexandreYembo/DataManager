using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Migration.Repository;
using Migration.Repository.Exceptions;
using Newtonsoft.Json.Linq;

namespace Migration.Infrastructure.CosmosDb
{
    public class CosmosDbGenericRepository : IGenericRepository
    {
        private readonly Container container;

        private readonly DBSettings _settings;

        public CosmosDbGenericRepository(DBSettings settings)
        {
            _settings = settings;

            var dbContextOptionsBuilder = new DbContextOptionsBuilder<DbContext>();
            dbContextOptionsBuilder.UseCosmos(
                accountEndpoint: settings.Endpoint,
                accountKey: settings.AuthKey,
                databaseName: settings.Database);

            var context = new DbContext(dbContextOptionsBuilder.Options);
            container = context.Database.GetCosmosClient()
                .GetContainer(settings.Database, settings.Container);
        }

        public async Task<Dictionary<string, string>> GetByListIds(string[] ids)
        {
            var query = $"select * from c where c.id IN({string.Join(',', ids.Select(v => "'" + v + "'"))})";

            return await Get(query);
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
                        dictionary[value.ToString()] = jToken.ToString(); ;
                    }
                }
            }

            return dictionary;
        }

        public async Task Update(JObject entity)
        {
            var response = await container.UpsertItemAsync(entity);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new DbOperationException(response.StatusCode.ToString(), entity.ToString());
            }
        }
    

        //public static class ObjectExtensions
        //{
        //    public static IDictionary<string, object> ToDictionary(this object obj)
        //    {
        //        return obj
        //            .GetType()
        //            .GetProperties()
        //            .ToDictionary(propertyInfo => propertyInfo.Name, propertyInfo => propertyInfo.GetValue(obj));
        //    }

        //}
    }
}