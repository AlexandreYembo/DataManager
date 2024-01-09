using Migration.Infrastructure.Redis.Entities;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;

namespace Migration.Infrastructure.Redis
{
    public interface IRepository<TEntity> where TEntity : class, new()
    {
        Task SaveAsync(RedisData<TEntity> redisData, string environment = "");
        Task SaveAsync(RedisData<JObject> redisData, string id);
        Task<List<JObject>> FindAsync(RedisData<JObject> redisData);
        Task<List<TEntity>> FindAsync(string key, string environment);
        Task<List<TEntity>> FindAsync(string environment = "");
        Task<RedisValue> FindByKeyAsync(RedisData<TEntity> redisData);
        Task<int> CountAsync(string key);
    }
}