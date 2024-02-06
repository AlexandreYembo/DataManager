using Migration.Repository.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Infrastructure.CosmosDb
{
    public class QueryBuilder
    {
        public static string Build(string rawQuery, List<DataFieldsMapping>? fieldMappings = null, string? data = null, int? take = 0, int skip = 0)
        {
            var joins = fieldMappings?.Where(w => w.MappingType == MappingType.TableJoin).ToList();

            string query;

            if (rawQuery.Contains("*") || rawQuery.Contains("c.id"))
                query = rawQuery;
            else
            {
                query = rawQuery.Replace("from c".ToLower(), ", c.id from c");
            }

            if (joins != null && joins.Any() && !string.IsNullOrEmpty(data))
            {
                if (!query.Contains("where")) //need to create where if does not exist because there are joins to be considered in the query
                {
                    query = query.Replace("from c".ToLower(), "from c where ");
                }
                else
                {
                    var whereValueRecovered = string.Empty;
                    if (query.IndexOf("order by", StringComparison.CurrentCultureIgnoreCase) > -1)
                    {
                        var query1 = query.Split("order by").FirstOrDefault();
                        whereValueRecovered = query1.Substring(query1.IndexOf("where", StringComparison.CurrentCultureIgnoreCase) + 5); //if there are already values for the where, need to recover and replace with and, because the list of joins will start the value after the where clause
                    }
                    else
                    {
                        whereValueRecovered = query.Substring(query.IndexOf("where", StringComparison.CurrentCultureIgnoreCase) + 5); //if there are already values for the where, need to recover and replace with and, because the list of joins will start the value after the where clause
                    }
                    query = query.Replace(whereValueRecovered, $"and ({whereValueRecovered})");
                }

                var relationshipData = JObject.Parse(data);

                var value = ConvertOperator(joins[0], relationshipData);

                if (!string.IsNullOrEmpty(value))
                    query = query.Replace("where", $"where {value} #joins# ");

                for (int i = 1; i < joins.Count; i++)
                {
                    value = ConvertOperator(joins[i], relationshipData);

                    if (!string.IsNullOrEmpty(value))
                        query = query.Replace("#joins#", $"and {value} #joins# ");
                }

                //Remove the mark after resolving all the joins from the Source table to the dynamic table
                query = query.Replace("#joins#", string.Empty);
            }

            if (take > 0) //pagination query
            {
                query += $" OFFSET {skip} LIMIT {take}";
            }

            return query;
        }

        //Convert operator to make dynamic queries
        private static string ConvertOperator(DataFieldsMapping dataFieldsMapping, JObject relationshipData)
        {
            var value = string.Empty;

            var fieldPath = relationshipData.SelectToken(dataFieldsMapping.SourceField);

            if (fieldPath.Type == JTokenType.String)
            {
                value = $"'{fieldPath}'";
            }
            else if (fieldPath.Type == JTokenType.Array)
            {
                for (int i = 0; i < fieldPath.Count(); i++)
                {
                    value += ",'" + fieldPath[i] + "'";
                }

                value = value.Substring(1);
            }
            else if (fieldPath.Type == JTokenType.Object)
            {
                //Todo
                throw new NotImplementedException("Method not implemented yet");
            }
            else
            {
                value = $"{fieldPath}";
            }

            var upperCaseOperation = string.Empty;

            if (dataFieldsMapping.IgnoreCaseSensitive)
            {
                upperCaseOperation = "lower(#value#)";
            }

            if (string.IsNullOrEmpty(value))
                return string.Empty;

            switch (dataFieldsMapping.OperatorType)
            {
                case OperatorType.ArrayContains: return $"ARRAY_CONTAINS(c.{dataFieldsMapping.DestinationField},{value})";
                case OperatorType.Eq:
                    if (!string.IsNullOrEmpty(upperCaseOperation))
                        return $"{upperCaseOperation.Replace("#value#", "c." + dataFieldsMapping.DestinationField)} = {upperCaseOperation.Replace("#value#", value)}";

                    return $"c.{dataFieldsMapping.DestinationField} = {value}";
                case OperatorType.In: return $"c.{dataFieldsMapping.DestinationField} in({value})";
                default: return string.Empty;
            }
        }

        public static string BuildTop(string rawQuery, int number)
        {
            var query = $"select top {number} * from " + rawQuery.Substring(rawQuery.IndexOf("from", StringComparison.CurrentCultureIgnoreCase));
            return query;
        }
    }
}