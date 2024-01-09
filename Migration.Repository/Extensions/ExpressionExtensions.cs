using Migration.Repository.Models;
using Newtonsoft.Json.Linq;
using System;

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
        public static List<JObject> ApplyJoin(this Dictionary<string, string> destination, KeyValuePair<string, string> source, List<DataFieldsMapping> propertiesMap)
        {
            Func<JObject, JObject, bool> comparisonFunc = (d, s) =>
            {
                foreach (var property in propertiesMap.Where(w => w.MappingType == MappingType.TableJoin))
                {
                    var value1 = s[property.SourceField]?.ToString();

                    if (string.IsNullOrEmpty(value1))
                        value1 = s.SelectToken(property.SourceField).ToString();

                    var field = property.DestinationField;

                    int? index = 0;

                    if (field.Contains("[") && field.Contains("]"))
                    {
                        var firstIndex = field.LastIndexOf("[", StringComparison.Ordinal) + 1;
                        var lastIndex = field.IndexOf("]", StringComparison.Ordinal);

                        if (lastIndex - firstIndex > 0)
                        {
                            var r = field.Substring(firstIndex, lastIndex - firstIndex);
                            index = int.Parse(r);

                            field = field.Substring(0, firstIndex - 1);
                        }
                    }

                    var path2 = d[field];

                    if (path2 == null)
                        path2 = d.SelectToken(field);

                    string value2 = "";

                    if (path2.GetType() == typeof(JArray))
                    {
                        var arr = ((JArray)d[field]);
                        var jtoken = arr[index];

                        value2 = jtoken.ToString();

                        if (!value2.Contains(value1, StringComparison.InvariantCultureIgnoreCase))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        value2 = d[property.DestinationField]?.ToString();

                        if (string.IsNullOrEmpty(value2))
                            value2 = d.SelectToken(property.DestinationField).ToString();

                        if (property.IgnoreCaseSensitive)
                        {
                            if (!value1.Equals(value2, StringComparison.InvariantCultureIgnoreCase))
                            {
                                return false;
                            }
                        }
                        else
                        {
                            if (value1 != value2)
                            {
                                return false;
                            }
                        }
                    }

                }
                return true;
            };

            var listSource = new List<JObject>() { JObject.Parse(source.Value) };

            var listDestination = destination.Select(s => JObject.Parse(s.Value)).ToList();

            return listDestination.Where(d => listSource.Any(s => comparisonFunc(d, s))).ToList();
        }
    }
}