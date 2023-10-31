using Migration.Repository.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Repository.Extensions
{
    public static class ExpressionExtensions
    {
        /// <summary>
        /// Apply join between tables, it will remove records that does not have the relationship.
        /// It only works if there is a mapping already defined as MappingType.tableJoin
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        /// <param name="propertiesMap"></param>
        /// <returns></returns>
        public static List<JObject> ApplyJoin(this Dictionary<string, string> destination, Dictionary<string, string> source, List<DataFieldsMapping> propertiesMap)
        {
            Func<JObject, JObject, bool> comparisonFunc = (d, s) =>
            {
                foreach (var property in propertiesMap.Where(w => w.MappingType == MappingType.tableJoin))
                {
                    var value1 = s[property.SourceField]?.ToString();

                    var path2 = d[property.DestinationField];

                    string value2 = "";

                    if (path2.GetType() == typeof(JArray))
                    {
                        value2 = d[property.DestinationField]?.ToString();

                        if (!value2.Contains(value1))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        value2 = d[property.DestinationField]?.ToString();

                        if (value1 != value2)
                        {
                            return false;
                        }
                    }

                }
                return true;
            };

            var listSource = source.Select(s => JObject.Parse(s.Value)).ToList();
            var listDestination = destination.Select(s => JObject.Parse(s.Value)).ToList();

            return listDestination.Where(d => listSource.Any(s => comparisonFunc(d, s))).ToList();
        }
    }
}