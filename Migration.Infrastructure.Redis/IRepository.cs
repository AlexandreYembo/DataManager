using Migration.Infrastructure.Redis.Entities;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;

namespace Migration.Infrastructure.Redis
{
    public interface IRepository<TEntity> where TEntity : class, new()
    {
        Task SaveAsync(RedisData<TEntity> redisData);
        Task SaveAsync(RedisData<JObject> redisData, string id);

        Task<List<TEntity>> FindAsync(string key);
        Task<List<TEntity>> FindAsync();
    }
}
