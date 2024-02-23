using Migration.Models;
using Migration.Models.Profile;
using Migration.Services.Extensions;
using Newtonsoft.Json.Linq;

namespace Migration.Services.Helpers
{
    public static class UpdateDataHelper
    {
        public static JObject UpdateObject(string sourceObj, List<DataFieldsMapping> mappingMergeFields, ref bool hasChange)
        {
            var objectToBeUpdated = JObject.Parse(sourceObj);

            foreach (var mappingMergeField in mappingMergeFields.Where(w => w.MappingType != MappingType.TableJoin))
            {
                if (mappingMergeField.MappingType == MappingType.MergeFieldWithCondition ||
                    mappingMergeField.MappingType == MappingType.UpdateValueWithCondition)
                {
                    var meetCriteria =
                        objectToBeUpdated.MeetCriteriaSearch(mappingMergeField.Conditions.Select(s => s));

                    if (meetCriteria)
                    {
                        var fieldArr = mappingMergeField.SourceField.Split(".").ToList();

                        objectToBeUpdated = JObjectHelper.UpdateObject(objectToBeUpdated, fieldArr, mappingMergeField.ValueField);

                        hasChange = true;
                    }
                }
                else if (mappingMergeField.MappingType == MappingType.UpdateValue)
                {
                    var newValue = MapFieldTypesHelper.GetType(mappingMergeField);

                    var fieldArr = mappingMergeField.TargetField.Split(".").ToList();

                    objectToBeUpdated = JObjectHelper.UpdateObject(objectToBeUpdated, fieldArr, newValue);
                    hasChange = true;
                }
                else
                {
                    var valueFromSource = JObjectHelper.GetValueFromObject(objectToBeUpdated, mappingMergeField.SourceField.Split(".").ToList());

                    var fieldsFromDestinationArr = mappingMergeField.TargetField.Split(".").ToList();

                    objectToBeUpdated = JObjectHelper.UpdateObject(objectToBeUpdated, fieldsFromDestinationArr, valueFromSource);
                    hasChange = true;
                }
            }

            return objectToBeUpdated;
        }

        public static JObject UpdateObject(string destinationData, List<DataFieldsMapping> mappingMergeFields, JObject sourceObj, ref bool hasChange)
        {
            var objectToBeUpdated = JObject.Parse(destinationData);

            foreach (var mappingMergeField in mappingMergeFields.Where(w => w.MappingType != MappingType.TableJoin))
            {
                if (mappingMergeField.MappingType == MappingType.MergeFieldWithCondition ||
                    mappingMergeField.MappingType == MappingType.UpdateValueWithCondition)
                {
                    var sourceConditions = mappingMergeField.Conditions
                        .Where(w => w.ConditionDirection == MappingDirectionType.Source).Select(s => s);

                    var targetConditions = mappingMergeField.Conditions
                        .Where(w => w.ConditionDirection == MappingDirectionType.Target).Select(s => s);


                    var meetCriteria = sourceConditions.Any(a => a.Type == SearchConditionType.Or) || targetConditions.Any(a => a.Type == SearchConditionType.Or)
                            ? sourceObj.MeetCriteriaSearch(sourceConditions) || objectToBeUpdated.MeetCriteriaSearch(targetConditions)
                            : sourceObj.MeetCriteriaSearch(sourceConditions) && objectToBeUpdated.MeetCriteriaSearch(targetConditions);

                    if (meetCriteria)
                    {
                        var fieldsArr = mappingMergeField.TargetField.Split(".").ToList();

                        var value = mappingMergeField.MappingType == MappingType.UpdateValueWithCondition
                            ? MapFieldTypesHelper.GetType(mappingMergeField)
                            : JObjectHelper.GetValueFromObject(sourceObj, mappingMergeField.SourceField.Split(".").ToList());

                        objectToBeUpdated = JObjectHelper.UpdateObject(objectToBeUpdated, fieldsArr, value);
                        hasChange = true;
                    }
                }
                else if (mappingMergeField.MappingType == MappingType.UpdateValue)
                {
                    var newValue = MapFieldTypesHelper.GetType(mappingMergeField);

                    var fieldsFromDestinationArr = mappingMergeField.TargetField.Split(".").ToList();

                    objectToBeUpdated = JObjectHelper.UpdateObject(objectToBeUpdated, fieldsFromDestinationArr, newValue);
                    hasChange = true;
                }
                else
                {
                    var valueFromSource =
                        JObjectHelper.GetValueFromObject(sourceObj, mappingMergeField.SourceField.Split(".").ToList());

                    var fieldsFromDestinationArr = mappingMergeField.TargetField.Split(".").ToList();

                    objectToBeUpdated = JObjectHelper.UpdateObject(objectToBeUpdated, fieldsFromDestinationArr, valueFromSource);
                    hasChange = true;
                }
            }

            return objectToBeUpdated;
        }
    }
}
