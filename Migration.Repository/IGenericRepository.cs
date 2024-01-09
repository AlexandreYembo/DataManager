using Migration.Repository.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Repository
{
    public interface IGenericRepository
    {
        Task<Dictionary<string, string>> Get(string query);
        Task<Dictionary<string, string>> Get(string rawQuery, List<DataFieldsMapping> fieldMappings, string data, int take, int skip);
        Task Update(JObject entity, List<DataFieldsMapping> fieldMappings = null);
        Task Delete(JObject entity);
    }
}