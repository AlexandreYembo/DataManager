using Migration.Repository.Models;

namespace Migration.Repository.Validations
{
    public class ProfileValidation
    {
        public bool IsValid(DataMapping dataMapping, int step)
        {
            ValidationMessages = new();
            switch (step)
            {
                case 1:
                    ValidationMessages = new();
                    return true;
                case 2: // DataSettings
                    return HasDataSettingValid(dataMapping);
                case 3: // Mappings
                    if (dataMapping.OperationType == OperationType.Import)
                    {
                        return HasValidAttributesConfiguration(dataMapping);
                    }

                    if (dataMapping.OperationType == OperationType.Report)
                    {
                        return true;
                    }
                    return HasValidMapping(dataMapping);
                case 4:
                    if (dataMapping.OperationType == OperationType.Import)
                    {
                        return HasValidMapping(dataMapping);
                    }

                    return true;
                default:
                    return true;
            }
        }

        private bool HasValidAttributesConfiguration(DataMapping dataMapping)
        {
            if (dataMapping.Destination.Settings.ConnectionType == ConnectionType.CosmosDb)
            {
                if (!dataMapping.Destination.Settings.CurrentEntity.Attributes.Any(f => f.Key == TableAttributesType.PartitionKey.ToString())
                    || string.IsNullOrWhiteSpace(dataMapping.Destination.Settings.CurrentEntity.Attributes.FirstOrDefault(f => f.Key == "PartitionKey").Value))
                {
                    ValidationMessages.Add(new() { Message = "Provide the Partition Key" });
                }

                if (!dataMapping.Destination.Settings.CurrentEntity.Attributes.Any(f => f.Key == TableAttributesType.RecordId.ToString())
                    || string.IsNullOrWhiteSpace(dataMapping.Destination.Settings.CurrentEntity.Attributes.FirstOrDefault(f => f.Key == "RecordId").Value))
                {
                    ValidationMessages.Add(new() { Message = "Specify the id of the record" });
                }
            }

            if (dataMapping.Destination.Settings.ConnectionType == ConnectionType.TableStorage)
            {
                if (!dataMapping.Destination.Settings.CurrentEntity.Attributes.Any(f => f.Key == TableAttributesType.PartitionKey.ToString())
                    || string.IsNullOrWhiteSpace(dataMapping.Destination.Settings.CurrentEntity.Attributes.FirstOrDefault(f => f.Key == "PartitionKey").Value))
                {
                    ValidationMessages.Add(new() { Message = "Provide the Partition Key and the Property that will be used" });
                }

                if (!dataMapping.Destination.Settings.CurrentEntity.Attributes.Any(f => f.Key == TableAttributesType.RowKey.ToString())
                    || string.IsNullOrWhiteSpace(dataMapping.Destination.Settings.CurrentEntity.Attributes.FirstOrDefault(f => f.Key == "RowKey").Value))
                {
                    ValidationMessages.Add(new() { Message = "Provide the Row Key and the Property that will be used" });
                }

                if (!dataMapping.Destination.Settings.CurrentEntity.Attributes.Any(f => f.Key == TableAttributesType.RecordId.ToString())
                    || string.IsNullOrWhiteSpace(dataMapping.Destination.Settings.CurrentEntity.Attributes.FirstOrDefault(f => f.Key == "RecordId").Value))
                {
                    ValidationMessages.Add(new() { Message = "Specify the id of the record" });
                }
            }


            return ValidationMessages.Count == 0;
        }

        private bool HasDataSettingValid(DataMapping dataMapping)
        {
            bool hasDataSource = !string.IsNullOrEmpty(dataMapping.Source.Settings.CurrentEntity.Name);

            if (!hasDataSource)
                ValidationMessages.Add(new() { Message = "Data settings for Source must be provided" });

            bool hasSourceQuery = !string.IsNullOrEmpty(dataMapping.Source.Query);

            if (!hasSourceQuery)
                ValidationMessages.Add(new() { Message = "Query for Source must be provided" });


            if (dataMapping.DataQueryMappingType == DataQueryMappingType.UpdateAnotherCollection)
            {
                bool hasDataDestination = !string.IsNullOrEmpty(dataMapping.Destination.Settings.CurrentEntity.Name);
                bool hasDestinationQuery = dataMapping.OperationType == OperationType.Import || !string.IsNullOrEmpty(dataMapping.Destination.Query);

                if (!hasDataDestination)
                    ValidationMessages.Add(new() { Message = "Data settings for Destination must be provided" });

                if (!hasDestinationQuery)
                    ValidationMessages.Add(new() { Message = "Query for Destination must be provided" });

                return (hasDataSource && hasDataDestination && hasSourceQuery && hasDestinationQuery);
            }

            return hasDataSource;
        }

        private bool HasValidMapping(DataMapping dataMapping)
        {
            if (dataMapping.DataQueryMappingType == DataQueryMappingType.UpdateAnotherCollection)
            {
                if (dataMapping.OperationType != OperationType.Import && !dataMapping.FieldsMapping.Any(a => a.MappingType == MappingType.TableJoin))
                {
                    ValidationMessages.Add(new() { Message = "A join between table must be provided when you are working with 2 tables." });
                }

                if (dataMapping.OperationType != OperationType.Delete && (!dataMapping.FieldsMapping.Any(a => a.MappingType == MappingType.MergeField || a.MappingType == MappingType.MergeFieldWithCondition || a.MappingType == MappingType.UpdateValue || a.MappingType == MappingType.UpdateValueWithCondition)))
                {
                    ValidationMessages.Add(new() { Message = "At least one type of mapping you need to provide in order to update the record." });
                }
            }
            else
            {
                if (dataMapping.OperationType != OperationType.Delete && (!dataMapping.FieldsMapping.Any(a => a.MappingType == MappingType.UpdateValue || a.MappingType == MappingType.UpdateValueWithCondition)))
                {
                    ValidationMessages.Add(new() { Message = "At least one type of mapping you need to provide in order to update the record." });
                }
            }

            return !ValidationMessages.Any();
        }

        public List<ValidationMessage> ValidationMessages { get; set; } = new();
    }

    public class ValidationMessage
    {
        public string Message { get; set; }
    }
}
