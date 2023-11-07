using Migration.Services.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Services.Extensions
{
    public static class ParseDynamicDataExtensions
    {
        public static List<DynamicData> ToDynamicDataList(this Dictionary<string, string> dictionary, DataType type = DataType.Source)
        {
            return dictionary.Select(s => new DynamicData()
            {
                Id = s.Key,
                Data = s.Value,
                DataType = type
            }).ToList();
        }

        public static List<DynamicData> ToDynamicDataList(this KeyValuePair<string, string> keyValue, DataType type = DataType.Source)
        {
            return new List<DynamicData>()
            {
                new()
                {
                    Id = keyValue.Key,
                    Data = keyValue.Value,
                    DataType = type
                }
            };
        }

        public static List<DynamicData> ToDynamicDataList(this IEnumerable<JObject> destination, JObject source)
        {
            var result = destination.Select(s => new DynamicData()
            {
                Id = s["id"].ToString(),
                Data = s.ToString(),
                DataType = DataType.Destination
            }).ToList();

            result.Add(new DynamicData()
            {
                Id = source["id"].ToString(),
                Data = source.ToString(),
                DataType = DataType.Source
            });

            return result;
        }
    }
}