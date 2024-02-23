using System.Text.Json.Serialization;
using System.Text.Json;
using StackExchange.Redis;
using Newtonsoft.Json.Linq;
using Connectors.Redis.Models;

namespace Connectors.Redis
{
    public class Repository<TEntity> : IRepository<TEntity> where TEntity : class, new()
    {
        private readonly IDatabase _db;

        public Repository(IDatabase db)
        {
            _db = db;
        }

        public async Task SaveAsync(HashKeyRedisData<TEntity> redisData)
        {
            var entity = redisData.Data;

            JsonSerializerOptions _JsonSerializerOptions = new()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };

            var value = JsonSerializer.Serialize(entity, _JsonSerializerOptions);

            await _db.HashSetAsync(typeof(TEntity).Name + (!string.IsNullOrEmpty(redisData.RedisKey) ? "-" + redisData.RedisKey : ""), new[] { new HashEntry(redisData.RedisValue, value) });
        }

        public async Task SaveAsync(HashKeyRedisData<JObject> redisData)
        {
            var redisValue = redisData.Data;

            await _db.HashSetAsync(redisData.RedisKey, new[] { new HashEntry(redisData.RedisValue, redisValue.ToString()) });
        }

        public async Task<List<JObject>> FindAsync(HashKeyRedisData<JObject> redisData)
        {
            var redisResult = await _db.HashGetAllAsync(redisData.RedisKey);
            return redisResult.OrderBy(s => s.Key).Select(s => JObject.Parse(s.Value)).ToList();
        }

        public async Task<RedisValue> FindByKeyAsync(HashKeyRedisData<TEntity> redisData)
        {
            var redisResult = await _db.HashGetAsync(typeof(TEntity).Name + (!string.IsNullOrEmpty(redisData.RedisKey) ? "-" + redisData.RedisKey : ""), new RedisValue(redisData.RedisValue));
            return redisResult;
        }

        public async Task<List<TEntity>> FindAsync(string jobCategory = "")
        {
            var redisResult = await _db.HashGetAllAsync(typeof(TEntity).Name + (!string.IsNullOrEmpty(jobCategory) ? "-" + jobCategory : ""));

            List<TEntity?> result = redisResult.Select(s => JsonSerializer.Deserialize<TEntity>(s.Value, GetOptions())).ToList();

            return result ?? new();
        }

        public async Task<List<TEntity>> FindAsync(string key, string jobCategory)
        {
            var redisResult = await _db.HashGetAllAsync(typeof(TEntity).Name + (!string.IsNullOrEmpty(jobCategory) ? "-" + jobCategory : ""));

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
