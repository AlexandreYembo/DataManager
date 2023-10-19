namespace Migration.Repository
{
    public interface IStorage<TEntity> where TEntity : class
    {
        Task<TEntity> Get();

        Task<TEntity> GetByKey(string key);
        Task<TEntity?> Get(Func<TEntity, bool> predicate);
        Task<List<TEntity>> GetAll();
        Task Add(TEntity entity);
        Task Add(TEntity entity, string key);
        Task Add(List<TEntity> entity);
        Task Update(TEntity entity);
        Task Delete(TEntity entity);
        Task Delete(string key);
    }
}