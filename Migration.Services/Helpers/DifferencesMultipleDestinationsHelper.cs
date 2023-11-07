using Migration.Repository.Helpers;
using Migration.Repository.Models;
using Migration.Services.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Services.Helpers
{
    public static class DifferencesMultipleDestinationsHelper
    {
        public static Dictionary<string, List<Difference>> FindDifferences(List<DataMapping> dataMappings, List<DynamicData> data)
        {
            Dictionary<string, List<Difference>> result = new();

            var mappingMergeFields = dataMappings
                .SelectMany(s => s.FieldsMapping)
                .Where(w => w.MappingType == MappingType.valueMerge).ToList();

            if (!mappingMergeFields.Any()) return new();

            var source = data.Where(w => w.DataType == DataType.Source).ToList();
            var destination = data.Where(w => w.DataType == DataType.Destination);

            foreach (var s in source)
            {
                var sourceObj = JObject.Parse(s.Data);
               
                foreach (var d in destination)
                {
                    bool hasChange = false;

                    var originalData = JObject.Parse(d.Data);
                    var objectToBeUpdated = JObject.Parse(d.Data);

                    foreach (var mappingMergeField in mappingMergeFields)
                    {
                        var fieldsFromSourceArr = mappingMergeField.SourceField.Split(".").ToList();

                        var valueFromSource = JObjectHelper.GetValueFromObject(sourceObj, fieldsFromSourceArr);

                        var fieldsFromDestinationArr = mappingMergeField.DestinationField.Split(".").ToList();

                        objectToBeUpdated = JObjectHelper.GetObject(objectToBeUpdated, fieldsFromDestinationArr, valueFromSource);
                        hasChange = true;
                    }

                    if (!hasChange) continue;

                    result.Add(originalData["id"].ToString(), DifferenceHelper.FindDifferences(originalData, objectToBeUpdated));
                }
            }

            return result;
        }
    }
}