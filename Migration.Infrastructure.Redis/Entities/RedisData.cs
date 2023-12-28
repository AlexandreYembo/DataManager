
namespace Migration.Infrastructure.Redis.Entities
{
    public class RedisData<TEntity>
    {
        public string Key { get; set; }
        public TEntity Data { get; set; }
        public string Id { get; set; }
    }
}