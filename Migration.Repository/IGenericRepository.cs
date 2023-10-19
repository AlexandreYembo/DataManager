using Newtonsoft.Json.Linq;

namespace Migration.Repository
{
    public interface IGenericRepository
    {
        Task<Dictionary<string, string>> Get(string query);
        Task<Dictionary<string, string>> GetByListIds(string[] ids);
        Task Update(JObject entity);
    }
}