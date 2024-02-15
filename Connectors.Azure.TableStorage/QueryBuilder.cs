using Connectors.Azure.TableStorage.Extensions;
using Microsoft.WindowsAzure.Storage.Table;
using Migration.Core;
using Migration.Core.Exceptions;
using Migration.Models;
using Migration.Models.Profile;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace Connectors.Azure.TableStorage
{
    public static class QueryBuilder
    {
        public static string BuildCustomQuery(this CloudTable table, string rawQuery, List<DataFieldsMapping>? fieldMappings = null, JObject? relationshipData = null,
            int? take = null)
        {
            var joins = fieldMappings?.Where(w => w.MappingType == MappingType.TableJoin).ToList();


            if (joins != null && joins.Any() && relationshipData != null)
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

            var filters = querySplit.Split(new[] { " and ", " or " }, StringSplitOptions.RemoveEmptyEntries);

            string query = string.Empty;

            for (int f = 0; f < filters.Length; f++)
            {
                var condition = filters[f].Split(new[] { "=", "!=" }, StringSplitOptions.RemoveEmptyEntries);

                var propertyCondition = condition.FirstOrDefault().Trim();
                var valueCondition = RemoveSpaces(filters[f]).Replace(propertyCondition + "=", String.Empty).Replace(propertyCondition + "!=", String.Empty).Replace("'", string.Empty);

                string operation = string.Empty;

                if (RemoveSpaces(filters[f].Split("=")[0]) == propertyCondition)
                {
                    operation = QueryComparisons.Equal;
                }
                else if (RemoveSpaces(filters[f].Split("!=")[0]) == propertyCondition)
                {
                    operation = QueryComparisons.NotEqual;
                }

                if (string.IsNullOrEmpty(operation)) continue;

                EdmType propertyType;

                if (propertyCondition == Constants.PARTITION_KEY || propertyCondition == Constants.ROW_KEY || string.IsNullOrEmpty(propertyCondition))
                {
                    propertyType = EdmType.String;
                }
                else
                {
                    propertyType = queryResult.Results.FirstOrDefault().Properties.FirstOrDefault(w => w.Key == propertyCondition).Value.PropertyType;
                }

                var filterCondition = propertyType.ParseToString(propertyCondition, operation, valueCondition);

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

            return query;
        }

        private static string RemoveSpaces(string value)
        {
            return Regex.Replace(value, @"\s+", "");
        }

        /// <summary>
        /// Convert operator defined in the mapping to apply in the where condition
        /// </summary>
        /// <param name="dataFieldsMapping"></param>
        /// <param name="relationshipData"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="TableOperationException"></exception>
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
                case OperatorType.ArrayContains: 
                    throw new TableOperationException(DbOperationErrorCodeConstants.ARRAY_CONTAINS_OPERATION_NOT_SUPPORTED, "Array Contains operator is not supported by Table Storage. Choose another one.");
                
                case OperatorType.Eq:
                    return !string.IsNullOrEmpty(upperCaseOperation)
                        ? $"{upperCaseOperation.Replace("#value#", "c." + dataFieldsMapping.TargetField)} = {upperCaseOperation.Replace("#value#", value)}"
                        :$"c.{dataFieldsMapping.TargetField} = {value}";
                
                case OperatorType.In: return $"c.{dataFieldsMapping.TargetField} in({value})";
                
                default: return string.Empty;
            }
        }
    }
}