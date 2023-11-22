using Migration.Repository.Extensions;
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

            //Test conditon
            //TestMethodForCondition(dataMappings, data);

            var mappingMergeFields = dataMappings
                .SelectMany(s => s.FieldsMapping)
                .Where(w => w.MappingType != MappingType.TableJoin).ToList();


            //mappingMergeFields.AddRange(new List<DataFieldsMapping>(){
            //    new()
            //{
            //    MappingType = MappingType.ValueWithCondition,
            //    SourceCondition = new List<SearchCondition>()
            //    {
            //        new (){Query = "BootstrapProfile.IsBootstrap == true || BootstrapProfile.Mno == \\\"TestAlex\\\"\""},
            //        new (){Query = "Profiles.Any(IsBootstrap == true && BootstrapProfile.Mno == 'TestAlex')", Type = SearchConditionType.And},
            //    },
            //    DestinationField = "IsBootstrapProfile",
            //    ValueField = "true",
            //    ValueType = FieldValueType.Boolean
            //},
            //    new()
            //    {
            //        MappingType = MappingType.ValueWithCondition,
            //        SourceCondition = new List<SearchCondition>()
            //        {
            //            new (){Query = "BootstrapProfile.IsBootstrap == true && BootstrapProfile.Mno == \\\"Vodafone\\\"\""},
            //            new (){Query = "Profiles.Any(IsBootstrap == true && BootstrapProfile.Mno == 'TestAlex')", Type = SearchConditionType.Or},
            //        },
            //        DestinationField = "IsBootstrapProfile",
            //        ValueField = "false",
            //        ValueType = FieldValueType.Boolean
            //    },
            //    new()
            //    {
            //        MappingType = MappingType.ValueWithCondition,
            //        SourceCondition = new List<SearchCondition>()
            //        {
            //            new (){Query = "BootstrapProfile.IsBootstrap == true || BootstrapProfile.Mno == \\\"TestAlex\\\"\""},
            //            new (){Query = "Profiles.Any(IsBootstrap == true && MnoTargetProfile.Mno == 'TestAlex')", Type = SearchConditionType.Or},
            //        },
            //        DestinationField = "IsBootstrapProfile",
            //        ValueField = "false",
            //        ValueType = FieldValueType.Boolean
            //    }
            //});

            //mappingMergeFields.AddRange(new List<DataFieldsMapping>()
            //{
            //    new()
            //    {
            //        MappingType = MappingType.ValueMergeWithCondition,
            //        SourceCondition = new List<SearchCondition>()
            //        {
            //            new()
            //            {
            //                Query = "BootstrapProfile.IsBootstrap == true || BootstrapProfile.Mno == \\\"TestAlex\\\"\""
            //            },
            //            new()
            //            {
            //                Query = "Profiles.Any(IsBootstrap == true && BootstrapProfile.Mno == 'TestAlex')",
            //                Type = SearchConditionType.Or
            //            },
            //        },
            //        SourceField = "Profiles",
            //        DestinationField = "Profiles"
            //    }
            //});

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
                        if (mappingMergeField.MappingType == MappingType.ValueMergeWithCondition || mappingMergeField.MappingType == MappingType.ValueWithCondition)
                        {
                            var meetCriteria = sourceObj.MeetCriteriaSearch(mappingMergeField.SourceCondition.Select(s => s));

                            if (meetCriteria)
                            {
                                var fieldsArr = mappingMergeField.DestinationField.Split(".").ToList();

                                var value = mappingMergeField.MappingType == MappingType.ValueWithCondition
                                    ? MapFieldTypes.GetType(mappingMergeField)
                                    : JObjectHelper.GetValueFromObject(sourceObj,  mappingMergeField.SourceField.Split(".").ToList());

                                objectToBeUpdated = JObjectHelper.GetObject(objectToBeUpdated, fieldsArr, value);
                                hasChange = true;
                            }
                        }
                        else
                        {
                            var valueFromSource = JObjectHelper.GetValueFromObject(sourceObj,  mappingMergeField.SourceField.Split(".").ToList());

                            var fieldsFromDestinationArr = mappingMergeField.DestinationField.Split(".").ToList();

                            objectToBeUpdated = JObjectHelper.GetObject(objectToBeUpdated, fieldsFromDestinationArr, valueFromSource);
                            hasChange = true;
                        }
                    }

                    if (!hasChange) continue;

                    result.Add(originalData["id"].ToString(), DifferenceHelper.FindDifferences(originalData, objectToBeUpdated));
                }
            }

            return result;
        }

        //private static void TestMethodForCondition(List<DataMapping> dataMappings, List<DynamicData> data)
        //{
        //    var mappingMergeFields = new List<DataFieldsConditionMapping>()
        //    {
        //        new()
        //        {
        //            MappingType = MappingType.ValueMergeWithCondition,
        //            SourceCondition = new List<SearchCondition>()
        //            {
        //                new (){Query = "BootstrapProfile.IsBootstrap == true || BootstrapProfile.Mno == \\\"TestAlex\\\"\""},
        //                new (){Query = "Profiles.Any(IsBootstrap == true && BootstrapProfile.Mno == 'TestAlex')", Type = SearchConditionType.And},
        //            },
        //            DestinationField = "IsBootstrapProfile",
        //            ValueField = "true",
        //            ValueType = ValueType.Boolean
        //        },
        //        new()
        //        {
        //            MappingType = MappingType.ValueMergeWithCondition,
        //            SourceCondition = new List<SearchCondition>()
        //            {
        //                new (){Query = "BootstrapProfile.IsBootstrap == true && BootstrapProfile.Mno == \\\"Vodafone\\\"\""},
        //                new (){Query = "Profiles.Any(IsBootstrap == true && BootstrapProfile.Mno == 'TestAlex')", Type = SearchConditionType.Or},
        //            },
        //            DestinationField = "IsBootstrapProfile",
        //            ValueField = "false",
        //            ValueType = ValueType.Boolean
        //        },
        //        new()
        //        {
        //            MappingType = MappingType.ValueMergeWithCondition,
        //            SourceCondition = new List<SearchCondition>()
        //            {
        //                new (){Query = "BootstrapProfile.IsBootstrap == true || BootstrapProfile.Mno == \\\"TestAlex\\\"\""},
        //                new (){Query = "Profiles.Any(IsBootstrap == true && MnoTargetProfile.Mno == 'TestAlex')", Type = SearchConditionType.Or},
        //            },
        //            DestinationField = "IsBootstrapProfile",
        //            ValueField = "false",
        //            ValueType = ValueType.Boolean
        //        }
        //    };

        //    if (!mappingMergeFields.Any()) return;

        //    var source = data.Where(w => w.DataType == DataType.Source).ToList();
        //    var destination = data.Where(w => w.DataType == DataType.Destination);

        //    foreach (var s in source)
        //    {
        //        var sourceData = JObject.Parse(s.Data);

        //        foreach (var destinationData in destination)
        //        {
        //            bool hasChange = false;

        //            var objectToBeUpdated = JObject.Parse(destinationData.Data);


        //            foreach (var mappingMergeField in mappingMergeFields)
        //            {
        //                var meetCriteria = sourceData.MeetCriteriaSearch(mappingMergeField.SourceCondition.Select(s => s));

        //                if (meetCriteria)
        //                {
        //                    var fieldsArr = mappingMergeField.DestinationField.Split(".").ToList();

        //                    var value = MapFieldTypes.GetType(mappingMergeField);

        //                    objectToBeUpdated = JObjectHelper.GetObject(objectToBeUpdated, fieldsArr, value);
        //                }
        //            }

        //            if (!hasChange) continue;
        //        }
        //    }
        //}
    }
}