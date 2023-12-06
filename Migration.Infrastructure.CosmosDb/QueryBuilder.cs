using Migration.Repository.Extensions;
using Migration.Repository.Helpers;
using Migration.Repository.Models;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace Migration.Infrastructure.CosmosDb
{
    public class QueryBuilder
    {
        public static string Build(string rawQuery, List<DataFieldsMapping>? fieldMappings = null, string? data = null, int? take = null)
        {
            var joins = fieldMappings?.Where(w => w.MappingType == MappingType.TableJoin).ToList();

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

            if (joins != null && joins.Any() && !string.IsNullOrEmpty(data))
            {
                if (!query.Contains("where")) //need to create where if does not exist because there are joins to be considered in the query
                {
                    query = query.Replace("from c".ToLower(), "from c where ");
                }
                else
                {
                    var whereValueRecovered = query.Substring(query.IndexOf("where") + 5); //if there are already values for the where, need to recover and replace with and, because the list of joins will start the value after the where clause
                    query = query.Replace(whereValueRecovered, $"and {whereValueRecovered}");
                }

                var relationshipData = JObject.Parse(data);

                var whereData = ConvertOperator(joins[0], relationshipData);

                if (!string.IsNullOrEmpty(whereData))
                    query = query.Replace("where", $"where {whereData} #joins# ");

                for (int i = 1; i < joins.Count; i++)
                {
                    whereData = ConvertOperator(joins[i], relationshipData);

                    if (!string.IsNullOrEmpty(whereData))
                        query = query.Replace("#joins#", $"and {whereData} #joins# ");
                }

                //Remove the mark after resolving all the joins from the Source table to the dynamic table
                query = query.Replace("#joins#", string.Empty);
            }

            return query;
        }

        public static string BuildFromMultipleSources(string rawQuery, List<DataFieldsMapping>? fieldMappings = null, Dictionary<string, string>? data = null, int? take = null)
        {
            var joins = fieldMappings?.Where(w => w.MappingType == MappingType.TableJoin).ToList();

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

            if (joins != null && joins.Any() && data != null && data.Any())
            {
                if (!query.Contains(
                        "where")) //need to create where if does not exist because there are joins to be considered in the query
                {
                    query = query.Replace("from c".ToLower(), "from c where ");
                }
                else
                {
                    var whereValueRecovered =
                        query.Substring(query.IndexOf("where") +
                                        5); //if there are already values for the where, need to recover and replace with and, because the list of joins will start the value after the where clause
                    query = query.Replace(whereValueRecovered, $"and {whereValueRecovered}");
                }

                bool hasAddedJoin = false;

                var joinGrouped = joins
                    .Join(data,
                        j => j.SourceEntity,
                        d => d.Key.Split(":").FirstOrDefault(),
                        (p, d) => new { Mapping = p, Data = GetValue(d, p) })
                    .GroupBy(g => g.Mapping.SourceField)
                    .ToList();

                foreach (var group in joinGrouped)
                {
                    switch (group.ToList()[0].Mapping.OperatorType)
                    {
                        case OperatorType.EqAnd:
                            if (hasAddedJoin)
                            {
                                query = query.Replace("#joins#", $"and (c.{group.Key} = {string.Join($"and c.{group.Key} =", group.Select(s => s.Data))}) #joins#");
                            }
                            else
                            {
                                query = query.Replace("where", $"where (c.{group.Key} = {string.Join($"and c.{group.Key} =", group.Select(s => s.Data))}) #joins#");
                            }

                            hasAddedJoin = true;
                            break;
                        case OperatorType.EqOr:
                            if (hasAddedJoin)
                            {
                                query = query.Replace("#joins#", $"and (c.{group.Key} = {string.Join($"or c.{group.Key} =", group.Select(s => s.Data))}) #joins# ");
                            }
                            else
                            {
                                query = query.Replace("where", $"where (c.{group.Key} = {string.Join($"or c.{group.Key} = ", group.Select(s => s.Data))}) #joins# ");
                            }

                            hasAddedJoin = true;
                            break;
                        case OperatorType.In:
                            if (hasAddedJoin)
                            {
                                query = query.Replace("#joins#", $"and c.{group.Key} IN ({string.Join($",", group.Select(s => s.Data))}) #joins# ");
                            }
                            else
                            {
                                query = query.Replace("where", $"where c.{group.Key} IN ({string.Join($",", group.Select(s => s.Data))}) #joins# ");
                            }

                            hasAddedJoin = true;
                            break;
                        default:
                            break;
                    }

                    //Remove the mark after resolving all the joins from the Source table to the dynamic table
                }

                query = query.Replace("#joins#", string.Empty);
            }

            return query;
        }

        private static string GetValue(KeyValuePair<string, string> d, DataFieldsMapping p)
        {
            var value = JObject.Parse(d.Value).GetValueByType(p.SourceField);
            return value;
        }

        //Convert operator to make dynamic queries
        private static string ConvertOperator(DataFieldsMapping dataFieldsMapping, JObject relationshipData)
        {
            var value = string.Empty;

            var field = relationshipData[dataFieldsMapping.SourceField] != null
                ? (SourceField: dataFieldsMapping.SourceField, DestinationField: dataFieldsMapping.DestinationField)
                : (SourceField: dataFieldsMapping.DestinationField, DestinationField: dataFieldsMapping.SourceField);

            if (relationshipData[field.SourceField].Type == JTokenType.String)
            {
                value = $"'{relationshipData[field.SourceField]}'";
            }
            else if (relationshipData[field.SourceField].Type == JTokenType.Array)
            {
                for (int i = 0; i < relationshipData[field.SourceField].Count(); i++)
                {
                    value += ",'" + relationshipData[field.SourceField][i] + "'";
                }

                value = value.Substring(1);
            }
            else if (relationshipData[field.SourceField].Type == JTokenType.Object)
            {
                //Todo
                throw new NotImplementedException("Method not implemented yet");
            }
            else
            {
                value = $"{relationshipData[field.SourceField]}";
            }

            if (string.IsNullOrEmpty(value))
                return string.Empty;

            switch (dataFieldsMapping.OperatorType)
            {
                case OperatorType.ArrayContains: return $" and ARRAY_CONTAINS(c.{field.DestinationField},{value})";
                case OperatorType.Eq: return $"c.{field.DestinationField} = {value}";
                case OperatorType.In: return $"c.{field.DestinationField} in({value})";
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