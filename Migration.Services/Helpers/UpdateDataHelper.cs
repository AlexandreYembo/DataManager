using Migration.Repository.Extensions;
using Migration.Repository.Helpers;
using Migration.Repository.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Services.Helpers
{
    public static class UpdateDataHelper
    {
        public static JObject UpdateObject(string originalData, List<DataFieldsMapping> mappingMergeFields, JObject sourceObj, ref bool hasChange)
        {
            var objectToBeUpdated = JObject.Parse(originalData);

            foreach (var mappingMergeField in mappingMergeFields.Where(w => w.MappingType != MappingType.TableJoin))
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

            return objectToBeUpdated;
        }
    }
}
