using Migration.Repository.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Infrastructure.CosmosDb
{
    public class QueryBuilder
    {
        public static string Build(string rawQuery, List<DataFieldsMapping>? fieldMappings = null, IEnumerable<string>? values = null, int? take = null)
        {
            var joins = fieldMappings?.Where(w => w.MappingType == MappingType.tableJoin).ToList();

            string query;

            if (rawQuery.Contains("*") || rawQuery.Contains("c.id"))
                query = rawQuery;
            else
            {
                query = rawQuery.Replace("from c".ToLower(), ", c.id from c");
            }

            if (take != null)
            {
                query = query.Replace("select ", $"select top {take}");
            }

            if (joins != null && joins.Any() && values != null && values.Any())
            {
                if (!query.Contains("where")) //need to create where if does not exist because there are joins to ve considered in the query
                {
                    query = query.Replace("from c".ToLower(), "from c where ");
                }
                else
                {
                    var whereValueRecovered = query.Substring(query.IndexOf("where") + 5); //if there are already values for the where, need to recover and replace with and, because the list of joins will start the value after the where clause
                    query = query.Replace(whereValueRecovered, $"and {whereValueRecovered}");
                }

                var relationshipData = JArray.FromObject(values);

                query = query.Replace("where", $"where {ConvertOperator(joins[0], relationshipData)} #joins# ");

                for (int i = 1; i < joins.Count; i++)
                {
                    query = query.Replace("#joins#", $"and {ConvertOperator(joins[i], relationshipData)} #joins# ");
                }

                //Remove the mark after resolving all the joins from the Source table to the dynamic table
                query = query.Replace("#joins#", string.Empty);
            }

            return query;
        }

        //Convert operator to make dynamic queries
        private static string ConvertOperator(DataFieldsMapping dataFieldsMapping, JArray relationshipData)
        {
            var values = string.Join(",", relationshipData.Select(s =>
            {
                try
                {
                    if (JObject.Parse(s.ToString())[dataFieldsMapping.SourceField].Type == JTokenType.String)
                    {
                        return $"'{JObject.Parse(s.ToString())[dataFieldsMapping.SourceField]}'";
                    }

                    return $"{JObject.Parse(s.ToString())[dataFieldsMapping.SourceField]}";
                }
                catch
                {
                    return string.Empty;
                }
            }));

            switch (dataFieldsMapping.OperatorType)
            {
                case OperatorType.ArrayContains: return $"ARRAY_CONTAINS(c.{dataFieldsMapping.DestinationField},{values})";
                case OperatorType.Eq: return $"c.{dataFieldsMapping.DestinationField} = {values}";
                case OperatorType.In: return $"c.{dataFieldsMapping.DestinationField} in({values})";
                default: return string.Empty;
            }
        }

        public static string BuildTop(string rawQuery, int number)
        {
            var query = $"select top {number} * from " + rawQuery.Substring(rawQuery.IndexOf("from"));
            return query;
        }
    }
}