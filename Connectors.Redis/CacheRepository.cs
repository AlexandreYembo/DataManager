using Connectors.Redis.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Migration.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;

namespace Connectors.Redis
{
    public interface ICacheRepository
    {
        Task<DataSettings> TestConnection(DataSettings settings);
        Task InsertAsync(HashKeyRedisData<JObject> redisData);
        Task<Dictionary<string, JObject>> GetAsync(string key);
        Dictionary<string, JObject> GetFromCache(Dictionary<string, JObject> cachedData, RepositoryParameters repositoryParameters);
        Task<Dictionary<string, JObject>> GetHashAsync(RepositoryParameters repositoryParameters);
    }

    public class CacheRepository : ICacheRepository
    {
        private readonly IDatabase _db;
        private readonly IServer _server;
        public CacheRepository(IConnectionMultiplexer connectionMultiplexer, IServiceProvider serviceProvider)
        {
            _db = connectionMultiplexer.GetDatabase(1);

            var serviceConfig = serviceProvider.GetRequiredService<IConfiguration>() as IConfigurationRoot;

            _server = connectionMultiplexer.GetServer(serviceConfig.GetConnectionString("Redis"));
        }

        public async Task<DataSettings> TestConnection(DataSettings settings)
        {
            var keys = _server.Keys(1);

            if (keys != null)
            {
                foreach (var item in keys)
                {
                    settings.Entities.Add(new Entity
                    {
                        Name = item
                    });
                }
            }

            return settings;
        }

        public async Task InsertAsync(HashKeyRedisData<JObject> redisData)
        {
            await _db.HashSetAsync(redisData.RedisKey, new[] { new HashEntry(redisData.RedisValue, redisData.Data.ToString()) });
        }

        public async Task<Dictionary<string, JObject>> GetAsync(string key)
        {
            //For cache always use one id as join to search be used as hash key

            //Always use source field to have the proper value to be obtained in the hashkey
            var data = await _db.HashGetAllAsync(key);

            Dictionary<string, JObject> dictionary = new();

            if (data == null) return dictionary;

            foreach (var item in data)
            {
                var jObject = JObject.Parse(item.Value);

                if (jObject.SelectToken("id") == null)
                {
                    var id = Guid.NewGuid().ToString();
                    jObject.Add("id", id);
                    dictionary.Add(id, jObject);
                }
                else
                {
                    dictionary.Add(jObject.SelectToken("id").ToString(), jObject);
                }
            }
           
            return dictionary;
        }

        public async Task<Dictionary<string, JObject>> GetHashAsync(RepositoryParameters repositoryParameters)
        {
            var sourceData = repositoryParameters.Data;

            //For cache always use one id as join to search be used as hash key
            var fieldsMapping = repositoryParameters.FieldMappings.FirstOrDefault(w => w.MappingType == MappingType.TableJoin);

            //Always use source field to have the proper value to be obtained in the hashkey
            RedisValue data = await _db.HashGetAsync(repositoryParameters.Entity, sourceData.SelectToken(fieldsMapping.SourceField).ToString());

            Dictionary<string, JObject> dictionary = new();

            if (!data.HasValue) return dictionary;

            var jObject = JObject.Parse(data);

            if (jObject.SelectToken("id") == null)
            {
                var id = Guid.NewGuid().ToString();
                jObject.Add("id", id);
                dictionary.Add(id, jObject);
            }
            else
            {
                dictionary.Add(jObject.SelectToken("id").ToString(), jObject);
            }
            return dictionary;
        }

        public Dictionary<string, JObject> GetFromCache(Dictionary<string, JObject> cachedData, RepositoryParameters repositoryParameters)
        {
            var sourceData = repositoryParameters.Data;
            var skip = repositoryParameters.Pagination.Skip;
            var take = repositoryParameters.Pagination.Take;

            var data = cachedData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            var fieldsMapping = repositoryParameters.FieldMappings.Where(w => w.MappingType == MappingType.TableJoin);

            foreach (var field in fieldsMapping)
            {
                var relationship = sourceData.SelectToken(field.SourceField);

                data = data.Where(w => w.Value.SelectToken(field.TargetField).ToString() == relationship.ToString())
                            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }

            return data;
        }
    }
}