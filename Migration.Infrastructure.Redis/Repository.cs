using System.Text.Json.Serialization;
using System.Text.Json;
using StackExchange.Redis;
using Migration.Infrastructure.Redis.Entities;
using Newtonsoft.Json.Linq;

namespace Migration.Infrastructure.Redis
{
    public class Repository<TEntity> : IRepository<TEntity> where TEntity : class, new()
    {
        private readonly IDatabase _db;

        public Repository(IDatabase db)
        {
            _db = db;
        }

        public async Task SaveAsync(RedisData<TEntity> redisData)
        {
            var entity = redisData.Data;

            JsonSerializerOptions _JsonSerializerOptions = new()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var value = JsonSerializer.Serialize(entity, _JsonSerializerOptions);

            await _db.HashSetAsync(entity.GetType().Name, new[] { new HashEntry(redisData.Key, value) });
        }

        public async Task SaveAsync(RedisData<JObject> redisData, string id)
        {
            var redisValue = redisData.Data;

            await _db.HashSetAsync(id, new[] { new HashEntry(redisData.Key, redisValue.ToString()) });
        }

        public async Task<List<JObject>> FindAsync(RedisData<JObject> redisData)
        {
            var redisResult = await _db.HashGetAllAsync(redisData.Id);
            return redisResult.Select(s => JObject.Parse(s.Value.ToString())).ToList();
        }

        public async Task<List<TEntity>> FindAsync()
        {
            var redisResult = await _db.HashGetAllAsync(typeof(TEntity).Name);

            List<TEntity?> result = redisResult.Select(s => JsonSerializer.Deserialize<TEntity>(s.Value, GetOptions())).ToList();

            return result ?? new();
        }

        public async Task<List<TEntity>> FindAsync(string key)
        {
            var redisResult = await _db.HashGetAllAsync(typeof(TEntity).Name);

            List<TEntity?> result = redisResult.Where(w => w.Key == key).Select(s => JsonSerializer.Deserialize<TEntity>(s.Value, GetOptions())).ToList();

            return result ?? new();
        }

        public async Task<long> CountAsync(string key)
        {
            var count = await _db.KeyRefCountAsync(key);

            return count ?? 0;
        }

        private static JsonSerializerOptions GetOptions() =>
            new() { PropertyNameCaseInsensitive = true };
    }
}