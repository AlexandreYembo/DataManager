using Migration.Services.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Services.Extensions
{
    public static class ParseDynamicDataExtensions
    {
        public static List<DynamicData> ToDynamicDataList(this Dictionary<string, string> dictionary)
        {
            return dictionary.Select(s => new DynamicData()
            {
                Id = s.Key,
                Data = s.Value
            }).ToList();
        }

        public static List<DynamicData> ToDynamicDataList(this IEnumerable<JObject> listJObject)
        {
            return listJObject.Select(s => new DynamicData()
            {
                Id = s["id"].ToString(),
                Data = s.ToString()
            }).ToList();
        }
    }
}