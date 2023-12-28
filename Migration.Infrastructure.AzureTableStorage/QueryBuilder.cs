using Microsoft.WindowsAzure.Storage.Table;
using Migration.Repository.Models;
using Newtonsoft.Json.Linq;
using System.Globalization;
using Migration.Repository.Exceptions;

namespace Migration.Infrastructure.AzureTableStorage
{
    public class QueryBuilder
    {
        public static string Build(string rawQuery, CloudTable table, List<DataFieldsMapping>? fieldMappings = null, string? data = null,
            int? take = null)
        {
            var joins = fieldMappings?.Where(w => w.MappingType == MappingType.TableJoin).ToList();
            

            if (joins != null && joins.Any() && !string.IsNullOrEmpty(data))
            {
                if (!rawQuery.Contains("where")) //need to create where if does not exist because there are joins to be considered in the query
                {
                    rawQuery = rawQuery.Replace("from c".ToLower(), "from c where ");
                }
                else
                {
                    var whereValueRecovered = rawQuery.Substring(rawQuery.IndexOf("where") + 5); //if there are already values for the where, need to recover and replace with and, because the list of joins will start the value after the where clause
                    rawQuery = rawQuery.Replace(whereValueRecovered, $"and ({whereValueRecovered})");
                }

                var relationshipData = JObject.Parse(data);

                rawQuery = rawQuery.Replace("where", $"where {ConvertOperator(joins[0], relationshipData)} #joins# ");

                for (int i = 1; i < joins.Count; i++)
                {
                    rawQuery = rawQuery.Replace("#joins#", $"and {ConvertOperator(joins[i], relationshipData)} #joins# ");
                }

                //Remove the mark after resolving all the joins from the Source table to the dynamic table
                rawQuery = rawQuery.Replace("#joins#", string.Empty);
            }

            TableQuery<DynamicTableEntity> projectionQuery = new TableQuery<DynamicTableEntity>().Take(1);

            TableQuerySegment<DynamicTableEntity> queryResult = table.ExecuteQuerySegmentedAsync(projectionQuery, null).GetAwaiter().GetResult();
           
            var querySplit = rawQuery.Split("where").LastOrDefault().Replace("c.", string.Empty);

            var filters = querySplit.Split(new[] {" and ", " or " }, StringSplitOptions.RemoveEmptyEntries);

            string query = string.Empty;

            for (int f = 0; f < filters.Length; f++)
            {
                var condition = filters[f].Split(new[] { "==", "!=" }, StringSplitOptions.RemoveEmptyEntries);

                string operation = string.Empty;
               
                if (filters[f].Contains("=="))
                {
                    operation = QueryComparisons.Equal;
                }

                if (filters[f].Contains("!="))
                {
                    operation = QueryComparisons.NotEqual;
                }

                if (string.IsNullOrEmpty(operation)) continue;

                var filterCondition = ParseQuery(condition.FirstOrDefault().Trim(), operation, queryResult.Results.FirstOrDefault().Properties.FirstOrDefault(w => w.Key == condition.FirstOrDefault().Trim()).Value.PropertyType, condition.LastOrDefault().Trim());

                if (!string.IsNullOrEmpty(query))
                {
                    var queryValidation = querySplit.Split(" and ", StringSplitOptions.RemoveEmptyEntries);

                    if (queryValidation.Any(a => a == filters[f]))
                    {
                        query = TableQuery.CombineFilters(query, TableOperators.And, filterCondition);
                    }
                    else
                    {
                        query = TableQuery.CombineFilters(query, TableOperators.Or, filterCondition);
                    }
                }
                else
                {
                    query = filterCondition;
                }
            }

            //TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "0ba992a0-aabc-49ec-ae06-bf8c09b9c6b6"));

            return query;
        }

        private static string ConvertOperator(DataFieldsMapping dataFieldsMapping, JObject relationshipData)
        {
            var value = string.Empty;

            if (JObject.Parse(relationshipData.ToString())[dataFieldsMapping.SourceField].Type == JTokenType.String)
            {
                value = $"'{JObject.Parse(relationshipData.ToString())[dataFieldsMapping.SourceField]}'";
            }
            else if (JObject.Parse(relationshipData.ToString())[dataFieldsMapping.SourceField].Type == JTokenType.Array)
            {
                for (int i = 0; i < JObject.Parse(relationshipData.ToString())[dataFieldsMapping.SourceField].Count(); i++)
                {
                    value += ",'" + JObject.Parse(relationshipData.ToString())[dataFieldsMapping.SourceField][i] + "'";
                }

                value = value.Substring(1);
            }
            else if (JObject.Parse(relationshipData.ToString())[dataFieldsMapping.SourceField].Type == JTokenType.Object)
            {
                //Todo
                throw new NotImplementedException("Method not implemented yet");
            }
            else
            {
                value = $"{JObject.Parse(relationshipData.ToString())[dataFieldsMapping.SourceField]}";
            }

            var upperCaseOperation = string.Empty;

            if (dataFieldsMapping.IgnoreCaseSensitive)
            {
                upperCaseOperation = "lower(#value#)";
            }

            switch (dataFieldsMapping.OperatorType)
            {
                case OperatorType.ArrayContains: throw new DbOperationException("ERROR-001", "Array Contains operator is not supported by Table Storage. Choose another one.");
                case OperatorType.Eq: return $"{upperCaseOperation.Replace("#value#", "c." + dataFieldsMapping.DestinationField)} = {upperCaseOperation.Replace("#value#", value)}";
                case OperatorType.In: return $"c.{dataFieldsMapping.DestinationField} in({value})";
                default: return string.Empty;
            }
        }

        private static string ParseQuery(string propertyName, string operation, EdmType valueType, string value)
        {
            string str = "";
            switch (valueType)
            {
                case EdmType.Binary:
                    str = string.Format((IFormatProvider)CultureInfo.InvariantCulture, "X'{0}'", (object)value);
                    break;
                case EdmType.Boolean:
                case EdmType.Int32:
                    str = value;
                    break;
                case EdmType.DateTime:
                    str = string.Format((IFormatProvider)CultureInfo.InvariantCulture, "datetime'{0}'", (object)value);
                    break;
                case EdmType.Double:
                    str = int.TryParse(value, out int _) ? string.Format((IFormatProvider)CultureInfo.InvariantCulture, "{0}.0", (object)value) : value;
                    break;
                case EdmType.Guid:
                    str = string.Format((IFormatProvider)CultureInfo.InvariantCulture, "guid'{0}'", (object)value);
                    break;
                case EdmType.Int64:
                    str = string.Format((IFormatProvider)CultureInfo.InvariantCulture, "{0}L", (object)value);
                    break;
                default:
                    str = string.Format((IFormatProvider)CultureInfo.InvariantCulture, "'{0}'", (object)value.Replace("'", "''"));
                    break;
            }
            return string.Format((IFormatProvider)CultureInfo.InvariantCulture, "{0} {1} {2}", (object)propertyName, (object)operation, (object)str);
        }
    }
}