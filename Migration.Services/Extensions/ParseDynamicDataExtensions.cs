using Migration.Services.Models;

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
    }
}