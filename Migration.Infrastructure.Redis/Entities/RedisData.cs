
namespace Migration.Infrastructure.Redis.Entities
{
    public class RedisData<TEntity>
    {
        public string RedisValue { get; set; }
        public TEntity Data { get; set; }
        public string RedisKey { get; set; }
    }
}