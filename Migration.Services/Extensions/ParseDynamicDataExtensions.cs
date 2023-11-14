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

        public static List<DynamicData> ToDynamicDataList(this Dictionary<string, IEnumerable<JObject>> destination, Dictionary<string, JObject> source)
        {
            List<DynamicData> result = new();
            result.Add(new DynamicData()
            {
                Id = source.Values.FirstOrDefault()["id"].ToString(),
                Data = source.Values.FirstOrDefault().ToString(),
                DataType = DataType.Source,
                Entity = source.Keys.FirstOrDefault()
            });

            result.AddRange(destination.SelectMany(s => s.Value.Select((v => new DynamicData()
            {
                Id = v["id"].ToString(),
                Data = v.ToString(),
                DataType = DataType.Destination,
                Entity = s.Key
            }))).ToList());

            return result;
        }
    }
}