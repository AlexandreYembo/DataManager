using Migration.Repository.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Repository
{
    public interface IGenericRepository
    {
        Task<Dictionary<string, string>> Get(string query);
        Task<Dictionary<string, string>> Get(string rawQuery, List<DataFieldsMapping> fieldMappings, string data, int take);

        Task<Dictionary<string, string>> Get(string rawQuery, List<DataFieldsMapping> fieldMappings, Dictionary<string, string> data, int take);
        Task<Dictionary<string, string>> GetTop5(string rawQuery);
        Task<Dictionary<string, string>> GetByListIds(string[] ids);
        Task Update(JObject entity);
    }
}