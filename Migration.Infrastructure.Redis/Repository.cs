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

        public async Task SaveAsync(RedisData<TEntity> redisData, string environment = "")
        {
            var entity = redisData.Data;

            JsonSerializerOptions _JsonSerializerOptions = new()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };

            var value = JsonSerializer.Serialize(entity, _JsonSerializerOptions);

            if (string.IsNullOrEmpty(environment))
            {
                await _db.HashSetAsync(entity.GetType().Name, new[] { new HashEntry(redisData.Key, value) });
            }
            else
            {
                await _db.HashSetAsync($"{entity.GetType().Name}-{environment}", new[] { new HashEntry(redisData.Key, value) });
            }
        }

        public async Task SaveAsync(RedisData<JObject> redisData, string id)
        {
            var redisValue = redisData.Data;

            await _db.HashSetAsync(id, new[] { new HashEntry(redisData.Key, redisValue.ToString()) });
        }

        public async Task<List<JObject>> FindAsync(RedisData<JObject> redisData)
        {
            var redisResult = await _db.HashGetAllAsync(redisData.Id);
            return redisResult.OrderBy(s => s.Key).Select(s => JObject.Parse(s.Value.ToString())).ToList();
        }

        public async Task<RedisValue> FindByKeyAsync(RedisData<TEntity> redisData)
        {
            var redisResult = await _db.HashGetAsync(redisData.Id, new RedisValue(redisData.Key));
            return redisResult;
        }

        public async Task<List<TEntity>> FindAsync(string environment = "")
        {
            var redisResult = await _db.HashGetAllAsync(typeof(TEntity).Name + (!string.IsNullOrEmpty(environment) ? "-"+ environment : ""));

            List<TEntity?> result = redisResult.Select(s => JsonSerializer.Deserialize<TEntity>(s.Value, GetOptions())).ToList();

            return result ?? new();
        }

        public async Task<List<TEntity>> FindAsync(string key, string environment)
        {
            var redisResult = await _db.HashGetAllAsync(typeof(TEntity).Name + (!string.IsNullOrEmpty(environment) ? "-" + environment : ""));

            List<TEntity?> result = redisResult.Where(w => w.Key == key).Select(s => JsonSerializer.Deserialize<TEntity>(s.Value, GetOptions())).ToList();

            return result ?? new();
        }

        public async Task<int> CountAsync(string key)
        {
            var count = (await _db.HashGetAllAsync(key)).Length;
            return count;
        }

        private static JsonSerializerOptions GetOptions() =>
            new() { PropertyNameCaseInsensitive = true };
    }
}