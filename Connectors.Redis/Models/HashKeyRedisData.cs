namespace Connectors.Redis.Models
{
    public class HashKeyRedisData<TEntity>
    {
        public string RedisValue { get; set; }
        public TEntity Data { get; set; }
        public string RedisKey { get; set; }
    }
}