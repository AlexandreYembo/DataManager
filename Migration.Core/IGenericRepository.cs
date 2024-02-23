using Migration.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Core
{
    public interface IGenericRepository
    {
        Task<DataSettings> TestConnection();
        Task<Dictionary<string, JObject>> GetAsync(string query);
        Task<Dictionary<string, JObject>> GetAsync(RepositoryParameters parameters);
        Task UpdateAsync(RepositoryParameters parameters);
        Task DeleteAsync(RepositoryParameters parameters);
        Task InsertAsync(RepositoryParameters parameters);
        Task CreateTableAsync();
    }
}