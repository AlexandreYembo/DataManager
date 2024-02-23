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

        Task InsertAsync(RepositoryParameters parameters);
        Task<Dictionary<string, JObject>> GetAsync(string key);
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

        public async Task InsertAsync(RepositoryParameters parameters)
        {
            var entity = parameters.Entity;

            //await _db.StringAppendAsync(entity, parameters.Data.ToString());
            await _db.SetAddAsync(entity, parameters.Data.ToString());
        }

        public async Task<Dictionary<string, JObject>> GetAsync(string key)
        {
            var setMembers = await _db.SetMembersAsync(key);

            //var redisValue = await _db.StringGetAsync(key);

            //dynamic dynamicData = JsonConvert.DeserializeObject<dynamic>(redisValue);

            Dictionary<string, JObject> dictionary = new();

            foreach (var item in setMembers)
            {
                var jObject = JObject.Parse(item);

                dictionary.Add(jObject.SelectToken("id").ToString(), jObject);
            }

            return dictionary;
        }
    }
}