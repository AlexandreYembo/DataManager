using Migration.Models;
using Migration.Models.Profile;
using Migration.Services.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Services.Helpers
{
    public static class DifferencesTargetHelper
    {
        public static Dictionary<string, List<Difference>> FindDifferences(ProfileConfiguration profile, List<DynamicData> data)
        {
            Dictionary<string, List<Difference>> result = new();

            var mappingMergeFields = profile.FieldsMapping
                .Where(w => w.MappingType != MappingType.TableJoin).ToList();

            if (!mappingMergeFields.Any() && profile.OperationType == OperationType.Update && profile.DataQueryMappingType == DataQueryMappingType .SourceToTarget) return new();

            var source = data.Where(w => w.DataType == DataType.Source).ToList();
            var targets = data.Where(w => w.DataType == DataType.Destination);

            bool hasChange = false;

            foreach (var s in source)
            {
                var sourceObj = s.Data;

                if (profile.DataQueryMappingType == DataQueryMappingType.SourceToTarget)
                {
                    if (profile.OperationType == OperationType.Import)
                    {
                        var originalData = new JObject();

                        var objectToBeUpdated = UpdateDataHelper.UpdateObject("{}", mappingMergeFields, sourceObj, ref hasChange);
                        if (!hasChange) continue;

                        result.Add(s.Entity + originalData["id"], DifferenceHelper.FindDifferences(originalData, objectToBeUpdated));
                    }
                    else
                    {
                        foreach (var target in targets)
                        {
                            if (profile.OperationType == OperationType.Delete)
                            {
                                List<Difference> differences = new()
                                {
                                    new Difference()
                                    {
                                        Object1Value = target.Data.ToString(),
                                        OperationType = profile.OperationType
                                    }
                                };
                                result.Add(target.Entity + target.Data["id"], differences);
                            }
                            else
                            {
                                var objectToBeUpdated = UpdateDataHelper.UpdateObject(target.Data.ToString(), mappingMergeFields, sourceObj, ref hasChange);
                                if (!hasChange) continue;

                                result.Add(target.Entity + target.Data["id"], DifferenceHelper.FindDifferences(target.Data, objectToBeUpdated));
                            }
                        }
                    }
                }
                else
                {
                    if (profile.OperationType == OperationType.Delete)
                    {
                        List<Difference> differences = new()
                        {
                            new Difference()
                            {
                                Object1Value = s.Data.ToString(),
                                OperationType = profile.OperationType
                            }
                        };
                        result.Add(s.Entity + sourceObj["id"], differences);
                    }
                    else
                    {
                        var objectToBeUpdated = UpdateDataHelper.UpdateObject(s.Data.ToString(), profile.FieldsMapping, sourceObj, ref hasChange);
                        result.Add(s.Entity + sourceObj["id"], DifferenceHelper.FindDifferences(sourceObj, objectToBeUpdated));
                    }
                }
            }

            return result;
        }
    }
}