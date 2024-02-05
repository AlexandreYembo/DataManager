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

            if (!mappingMergeFields.Any() && dataMapping.OperationType == OperationType.Update && dataMapping.DataQueryMappingType == DataQueryMappingType.UpdateAnotherCollection) return new();

            var source = data.Where(w => w.DataType == DataType.Source).ToList();
            var destination = data.Where(w => w.DataType == DataType.Destination);

            bool hasChange = false;

            foreach (var s in source)
            {
                var sourceObj = JObject.Parse(s.Data);

                if (dataMapping.DataQueryMappingType == DataQueryMappingType.UpdateAnotherCollection)
                {
                    if (dataMapping.OperationType == OperationType.Import)
                    {
                        var originalData = new JObject();

                        var objectToBeUpdated = UpdateDataHelper.UpdateObject("{}", mappingMergeFields, sourceObj, ref hasChange);
                        if (!hasChange) continue;

                        result.Add(s.Entity + originalData["id"], DifferenceHelper.FindDifferences(originalData, objectToBeUpdated));
                    }
                    else
                    {
                        foreach (var d in destination)
                        {
                            var originalData = JObject.Parse(d.Data);

                            if (dataMapping.OperationType == OperationType.Delete)
                            {
                                List<Difference> differences = new()
                                {
                                    new Difference()
                                    {
                                        Object1Value = d.Data,
                                        OperationType = dataMapping.OperationType
                                    }
                                };
                                result.Add(d.Entity + originalData["id"], differences);
                            }
                            else
                            {
                                var objectToBeUpdated = UpdateDataHelper.UpdateObject(d.Data, mappingMergeFields, sourceObj, ref hasChange);
                                if (!hasChange) continue;

                                result.Add(d.Entity + originalData["id"], DifferenceHelper.FindDifferences(originalData, objectToBeUpdated));
                            }
                        }
                    }
                }
                else
                {
                    if (dataMapping.OperationType == OperationType.Delete)
                    {
                        List<Difference> differences = new()
                        {
                            new Difference()
                            {
                                Object1Value = s.Data,
                                OperationType = dataMapping.OperationType
                            }
                        };
                        result.Add(s.Entity + sourceObj["id"], differences);
                    }
                    else
                    {
                        var objectToBeUpdated = UpdateDataHelper.UpdateObject(s.Data, dataMapping.FieldsMapping, sourceObj, ref hasChange);
                        result.Add(s.Entity + sourceObj["id"], DifferenceHelper.FindDifferences(sourceObj, objectToBeUpdated));
                    }
                }
            }

            return result;
        }
    }
}