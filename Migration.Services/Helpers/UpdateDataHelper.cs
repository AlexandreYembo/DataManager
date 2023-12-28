using Migration.Repository.Extensions;
using Migration.Repository.Helpers;
using Migration.Repository.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Services.Helpers
{
    public static class UpdateDataHelper
    {
        public static JObject UpdateObject(string originalData, List<DataFieldsMapping> mappingMergeFields, JObject sourceObj, DataQueryMappingType dataQueryMappingType, ref bool hasChange)
        {
            var objectToBeUpdated = JObject.Parse(originalData);

            foreach (var mappingMergeField in mappingMergeFields.Where(w => w.MappingType != MappingType.TableJoin))
            {
                if (mappingMergeField.MappingType == MappingType.MergeFieldWithCondition ||
                    mappingMergeField.MappingType == MappingType.UpdateValueWithCondition)
                {
                    var meetCriteria = sourceObj.MeetCriteriaSearch(mappingMergeField.SourceCondition.Select(s => s));

                    if (meetCriteria)
                    {
                        var fieldsArr = mappingMergeField.DestinationField.Split(".").ToList();

                        var value = mappingMergeField.MappingType == MappingType.UpdateValueWithCondition
                            ? MapFieldTypes.GetType(mappingMergeField)
                            : JObjectHelper.GetValueFromObject(sourceObj, mappingMergeField.SourceField.Split(".").ToList());

                        objectToBeUpdated = JObjectHelper.GetObject(objectToBeUpdated, fieldsArr, value);
                        hasChange = true;
                    }
                }
                else if (mappingMergeField.MappingType == MappingType.UpdateValue)
                {
                    var newValue = MapFieldTypes.GetType(mappingMergeField);

                    var fieldsFromDestinationArr = dataQueryMappingType == DataQueryMappingType.UpdateAnotherCollection
                        ? mappingMergeField.DestinationField.Split(".").ToList()
                        : mappingMergeField.SourceField.Split(".").ToList();

                    objectToBeUpdated = JObjectHelper.GetObject(objectToBeUpdated, fieldsFromDestinationArr, newValue);
                    hasChange = true;
                }
                else
                {
                    var valueFromSource =
                        JObjectHelper.GetValueFromObject(sourceObj, mappingMergeField.SourceField.Split(".").ToList());

                    var fieldsFromDestinationArr = dataQueryMappingType == DataQueryMappingType.UpdateAnotherCollection
                        ? mappingMergeField.DestinationField.Split(".").ToList()
                            : mappingMergeField.SourceField.Split(".").ToList();

                    objectToBeUpdated = JObjectHelper.GetObject(objectToBeUpdated, fieldsFromDestinationArr, valueFromSource);
                    hasChange = true;
                }
            }

            return objectToBeUpdated;
        }
    }
}
