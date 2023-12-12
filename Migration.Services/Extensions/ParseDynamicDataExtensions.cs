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

        public static List<DynamicData> ToDynamicDataList(this KeyValuePair<string, JObject> keyValue, DataType type = DataType.Source)
        {
            return new List<DynamicData>()
            {
                new()
                {
                    Id = GetId(keyValue.Key),
                    Data = keyValue.Value.ToString(),
                    DataType = type,
                    Entity = GetEntity(keyValue.Key)
                }
            };
        }

        public static List<DynamicData> ToDynamicDataList(this Dictionary<string, IEnumerable<JObject>> destination, KeyValuePair<string, string> source, string sourceEntity)
        {
            List<DynamicData> result = new();
            result.Add(new DynamicData()
            {
                Id = JObject.Parse(source.Value)["id"].ToString(),
                Data = source.Value,
                DataType = DataType.Source,
                Entity = sourceEntity
            });

            result.AddRange(destination.SelectMany(s => s.Value.Select((v => new DynamicData()
            {
                Id = v["id"].ToString(),
                Data = v.ToString(),
                DataType = DataType.Destination,
                Entity = GetEntity(s.Key)
            }))).ToList());

            return result;
        }
        private static string? GetEntity(string key)
        {
            return key.Split(":").FirstOrDefault();
        }

        private static string? GetId(string key)
        {
            return key.Split(":").LastOrDefault();
        }
    }
}