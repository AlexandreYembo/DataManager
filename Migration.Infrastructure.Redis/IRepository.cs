using Migration.Infrastructure.Redis.Entities;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;

namespace Migration.Infrastructure.Redis
{
    public interface IRepository<TEntity> where TEntity : class, new()
    {
        Task SaveAsync(RedisData<TEntity> redisData);
        Task SaveAsync(RedisData<JObject> redisData);
        Task<List<JObject>> FindAsync(RedisData<JObject> redisData);
        Task<List<TEntity>> FindAsync(string key, string jobCategory);
        Task<List<TEntity>> FindAsync(string jobCategory = "");
        Task<RedisValue> FindByKeyAsync(RedisData<TEntity> redisData);
        Task<int> CountAsync(string key);
    }
}