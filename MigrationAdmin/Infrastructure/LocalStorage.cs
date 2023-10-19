using Blazored.LocalStorage;
using Migration.Repository;

namespace MigrationAdmin.Infrastructure
{
    public class LocalStorage<TEntity> : IStorage<TEntity> where TEntity : class
    {
        private readonly ILocalStorageService _localstorage;

        public LocalStorage(ILocalStorageService localStorage)
        {
            _localstorage = localStorage;
        }

        public async Task Add(TEntity entity)
        {
            await _localstorage.SetItemAsync(typeof(TEntity).Name, entity);
        }

        public async Task Add(TEntity entity, string key)
        {
            await _localstorage.SetItemAsync(key, entity);
        }

        public async Task Add(List<TEntity> entity)
        {
            await _localstorage.SetItemAsync(typeof(TEntity).Name, entity);
        }

        public async Task Delete(TEntity entity)
        {
            await _localstorage.RemoveItemAsync(typeof(TEntity).Name);
        }

        public async Task Delete(string key)
        {
            await _localstorage.RemoveItemAsync(key);
        }

        public async Task<TEntity> Get()
        {
           var result = await _localstorage.GetItemAsync<TEntity>(typeof(TEntity).Name);

           return result;
        }

        public async Task<TEntity> GetByKey(string key)
        {
            var result = await _localstorage.GetItemAsync<TEntity>(key);

            return result;
        }

        public async Task<TEntity?> Get(Func<TEntity, bool> predicate)
        {
            var result = await GetAll();

            return result.FirstOrDefault(predicate);
        }

        public async Task<List<TEntity>> GetAll()
        {
            try
            {
                var result = await _localstorage.GetItemAsync<List<TEntity>>(typeof(TEntity).Name);

                return result;
            }
            catch
            {
                return new List<TEntity>();
            }
        }

        public async Task Update(TEntity entity)
        {
            await _localstorage.SetItemAsync(typeof(TEntity).Name, entity);
        }
    }
}