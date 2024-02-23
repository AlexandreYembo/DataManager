using Connectors.Redis.Models;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;

namespace Connectors.Redis
{
    public interface IRepository<TEntity> where TEntity : class, new()
    {
        Task SaveAsync(HashKeyRedisData<TEntity> redisData);
        Task SaveAsync(HashKeyRedisData<JObject> redisData);
        Task<List<JObject>> FindAsync(HashKeyRedisData<JObject> redisData);
        Task<List<TEntity>> FindAsync(string key, string jobCategory);
        Task<List<TEntity>> FindAsync(string jobCategory = "");
        Task<RedisValue> FindByKeyAsync(HashKeyRedisData<TEntity> redisData);
        Task<int> CountAsync(string key);
    }
}