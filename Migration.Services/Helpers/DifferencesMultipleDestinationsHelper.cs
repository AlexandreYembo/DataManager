using Migration.Repository.Extensions;
using Migration.Repository.Helpers;
using Migration.Repository.Models;
using Migration.Services.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Services.Helpers
{
    public static class DifferencesMultipleDestinationsHelper
    {
        public static Dictionary<string, List<Difference>> FindDifferences(DataMapping dataMapping, List<DynamicData> data)
        {
            Dictionary<string, List<Difference>> result = new();

            var mappingMergeFields = dataMapping.FieldsMapping
                .Where(w => w.MappingType != MappingType.TableJoin).ToList();

            if (!mappingMergeFields.Any()) return new();

            var source = data.Where(w => w.DataType == DataType.Source).ToList();
            var destination = data.Where(w => w.DataType == DataType.Destination);

            foreach (var s in source)
            {
                var sourceObj = JObject.Parse(s.Data);

                if (dataMapping.Destination.Any(f => f.OperationType == OperationType.Insert))
                {
                    string json = "{}";
                    var jObject = JObject.Parse(json);

                    jObject["id"] = Guid.NewGuid();
                    var newRecord = new DynamicData()
                    {
                        Data = jObject.ToString()
                    };

                    if (!UpdatingObject(newRecord, mappingMergeFields, sourceObj, out var originalData, out var objectToBeUpdated)) continue;
                    result.Add(originalData["id"].ToString(), DifferenceHelper.FindDifferences(originalData, objectToBeUpdated));
                }
                else
                {
                    foreach (var d in destination)
                    {
                        if (!UpdatingObject(d, mappingMergeFields, sourceObj, out var originalData, out var objectToBeUpdated)) continue;

                        result.Add(originalData["id"].ToString(), DifferenceHelper.FindDifferences(originalData, objectToBeUpdated));
                    }
                }
            }

            return result;
        }

        private static bool UpdatingObject(DynamicData d, List<DataFieldsMapping> mappingMergeFields, JObject sourceObj, out JObject originalData,
            out JObject objectToBeUpdated)
        {
            bool hasChange = false;

            originalData = JObject.Parse(d.Data);
            objectToBeUpdated = JObject.Parse(d.Data);

            foreach (var mappingMergeField in mappingMergeFields)
            {
                if (mappingMergeField.MappingType == MappingType.FieldValueMergeWithCondition ||
                    mappingMergeField.MappingType == MappingType.ValueWithCondition)
                {
                    var meetCriteria = sourceObj.MeetCriteriaSearch(mappingMergeField.SourceCondition.Select(s => s));

                    if (meetCriteria)
                    {
                        var fieldsArr = mappingMergeField.DestinationField.Split(".").ToList();

                        var value = mappingMergeField.MappingType == MappingType.ValueWithCondition
                            ? MapFieldTypes.GetType(mappingMergeField)
                            : JObjectHelper.GetValueFromObject(sourceObj, mappingMergeField.SourceField.Split(".").ToList());

                        objectToBeUpdated = JObjectHelper.GetObject(objectToBeUpdated, fieldsArr, value);
                        hasChange = true;
                    }
                }
                else
                {
                    var valueFromSource =
                        JObjectHelper.GetValueFromObject(sourceObj, mappingMergeField.SourceField.Split(".").ToList());

                    var fieldsFromDestinationArr = mappingMergeField.DestinationField.Split(".").ToList();

                    objectToBeUpdated = JObjectHelper.GetObject(objectToBeUpdated, fieldsFromDestinationArr, valueFromSource);
                    hasChange = true;
                }
            }

            return hasChange;
        }
    }
}