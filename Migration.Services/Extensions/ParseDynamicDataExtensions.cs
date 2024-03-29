﻿using Migration.Models;
using Migration.Services.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Services.Extensions
{
    public static class ParseDynamicDataExtensions
    {
        public static List<DynamicData> ToDynamicDataList(this KeyValuePair<string, JObject> keyValue, DataType type = DataType.Source)
        {
            return new List<DynamicData>()
            {
                new()
                {
                    Id = GetId(keyValue.Key),
                    Data = keyValue.Value,
                    DataType = type,
                    Entity = GetEntity(keyValue.Key)
                }
            };
        }

        public static List<DynamicData> ToDynamicDataList(this Dictionary<string, IEnumerable<JObject>> destination, KeyValuePair<string, JObject> source, string sourceEntity, OperationType operationType)
        {
            List<ActionType> actionTypes = new();

            if (operationType == OperationType.Import)
            {
                actionTypes.Add(ActionType.Insert);
            }
            else if (operationType == OperationType.Delete)
            {
                actionTypes.Add(ActionType.Delete);
            }

            List<DynamicData> result = new();
            result.Add(new DynamicData()
            {
                Id = source.Value["id"] != null ? source.Value["id"].ToString() : source.Key,
                Data = source.Value,
                DataType = DataType.Source,
                Entity = sourceEntity
            });

            result.AddRange(destination.SelectMany(s => s.Value.Select((v => new DynamicData()
            {
                Id = v["id"].ToString(),
                Data = v,
                DataType = DataType.Destination,
                Entity = GetEntity(s.Key),
                Actions = actionTypes
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