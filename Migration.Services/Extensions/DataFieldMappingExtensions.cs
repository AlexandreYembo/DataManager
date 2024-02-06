using Migration.Repository.Models;

namespace Migration.Services.Extensions
{
    public static class DataFieldMappingExtensions
    {
        /// <summary>
        /// It will convert the custom type mappings (condition, update values) into Merge type because as we are reverting to the original data, we should merge fields, not check conditions or update constant values
        /// </summary>
        /// <param name="values"></param>
        /// <param name="fieldMappings"></param>
        public static List<DataFieldsMapping> RevertMapping(this List<DataFieldsMapping> values)
        {
            List<DataFieldsMapping> fieldMappings = new();
            foreach (var mapping in values
                         .Where(w => w.MappingType != MappingType.TableJoin))
            {
                if (mapping.MappingType == MappingType.MergeField ||
                    mapping.MappingType == MappingType.MergeFieldWithCondition)
                {
                    fieldMappings.Add(new DataFieldsMapping
                    {
                        MappingType = MappingType.MergeField,
                        DestinationField = mapping.DestinationField,
                        SourceField = mapping.DestinationField,
                        ValueType = mapping.ValueType,
                    });
                }
                else
                {
                    if (mapping.DestinationField != null &&
                        !fieldMappings.Any(a => a.DestinationField == mapping.DestinationField))
                    {
                        fieldMappings.Add(new DataFieldsMapping
                        {
                            MappingType = MappingType.MergeField,
                            DestinationField = mapping.DestinationField,
                            SourceField = mapping.DestinationField,
                            ValueType = mapping.ValueType,
                        });
                    }


                    if (mapping.SourceField != null && !fieldMappings.Any(a => a.SourceField == mapping.SourceField))
                    {
                        fieldMappings.Add(new DataFieldsMapping
                        {
                            MappingType = MappingType.MergeField,
                            DestinationField = mapping.SourceField,
                            SourceField = mapping.SourceField,
                            ValueType = mapping.ValueType,
                        });
                    }
                }
            }

            return fieldMappings;
        }
    }
}