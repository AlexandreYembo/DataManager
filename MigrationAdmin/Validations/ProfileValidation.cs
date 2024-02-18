using Migration.EventHandlers;
using Migration.Models;
using Migration.Models.Profile;
using OperationType = Migration.Models.OperationType;

namespace MigrationAdmin.Validations
{
    public class ProfileValidation
    {
        private readonly ValidationMessagePublisher _publisher;

        public ProfileValidation(ValidationMessagePublisher publisher)
        {
            _publisher = publisher;
        }

        public bool IsValid(ProfileConfiguration profile, int step)
        {
            ValidationMessages = new();
            switch (step)
            {
                case 2: // DataSettings
                    HasDataSettingValid(profile);
                    break;
                case 3: // Mappings
                    HasValidMapping(profile);
                    break;
                case 4:
                    if (profile.OperationType == OperationType.Import)
                    {
                        HasValidAttributesConfiguration(profile);
                    }
                    break;
                default:
                    break;
            }

            if (ValidationMessages.Any())
            {
                _publisher.Publish(ValidationMessages);
                return false;
            }

            return true;
        }

        private void HasValidAttributesConfiguration(ProfileConfiguration profile)
        {
            if (profile.Target.Settings.ConnectionType == ConnectionType.CosmosDb)
            {
                if (!profile.Target.Settings.CurrentEntity.Attributes.Any(f => f.Key == TableAttributesType.PartitionKey.ToString())
                    || string.IsNullOrWhiteSpace(profile.Target.Settings.CurrentEntity.Attributes.FirstOrDefault(f => f.Key == "PartitionKey").Value))
                {
                    ValidationMessages.Add(new() { Message = "Provide the Partition Key" });
                }

                if (!profile.Target.Settings.CurrentEntity.Attributes.Any(f => f.Key == TableAttributesType.RecordId.ToString())
                    || string.IsNullOrWhiteSpace(profile.Target.Settings.CurrentEntity.Attributes.FirstOrDefault(f => f.Key == "RecordId").Value))
                {
                    ValidationMessages.Add(new() { Message = "Specify the id of the record" });
                }
            }

            if (profile.Target.Settings.ConnectionType == ConnectionType.TableStorage)
            {
                if (!profile.Target.Settings.CurrentEntity.Attributes.Any(f => f.Key == TableAttributesType.PartitionKey.ToString())
                    || string.IsNullOrWhiteSpace(profile.Target.Settings.CurrentEntity.Attributes.FirstOrDefault(f => f.Key == "PartitionKey").Value))
                {
                    ValidationMessages.Add(new() { Message = "Provide the Partition Key and the Property that will be used" });
                }

                if (!profile.Target.Settings.CurrentEntity.Attributes.Any(f => f.Key == TableAttributesType.RowKey.ToString())
                    || string.IsNullOrWhiteSpace(profile.Target.Settings.CurrentEntity.Attributes.FirstOrDefault(f => f.Key == "RowKey").Value))
                {
                    ValidationMessages.Add(new() { Message = "Provide the Row Key and the Property that will be used" });
                }

                if (!profile.Target.Settings.CurrentEntity.Attributes.Any(f => f.Key == TableAttributesType.RecordId.ToString())
                    || string.IsNullOrWhiteSpace(profile.Target.Settings.CurrentEntity.Attributes.FirstOrDefault(f => f.Key == "RecordId").Value))
                {
                    ValidationMessages.Add(new() { Message = "Specify the id of the record" });
                }
            }
        }

        private void HasDataSettingValid(ProfileConfiguration profile)
        {
            bool hasSourceData = !string.IsNullOrEmpty(profile.Source.Settings.CurrentEntity.Name);

            if (!hasSourceData)
                ValidationMessages.Add(new() { Message = "Data settings for Source must be provided" });

            bool hasSourceQuery = !string.IsNullOrEmpty(profile.Source.Query);

            if (!hasSourceQuery)
                ValidationMessages.Add(new() { Message = "Query for Source must be provided" });


            if (profile.DataQueryMappingType == DataQueryMappingType.SourceToTarget)
            {
                bool hasTargetData = !string.IsNullOrEmpty(profile.Target.Settings.CurrentEntity.Name);
                bool hasTargetQuery = profile.OperationType == OperationType.Import || !string.IsNullOrEmpty(profile.Target.Query);

                if (!hasTargetData)
                    ValidationMessages.Add(new() { Message = "Data settings for Target must be provided" });

                if (!hasTargetQuery)
                    ValidationMessages.Add(new() { Message = "Query for Target must be provided" });
            }
        }

        private void HasValidMapping(ProfileConfiguration profile)
        {
            if (profile.DataQueryMappingType == DataQueryMappingType.SourceToTarget)
            {
                if (profile.OperationType != OperationType.Import && !profile.FieldsMapping.Any(a => a.MappingType == MappingType.TableJoin))
                {
                    ValidationMessages.Add(new() { Message = "A join between table must be provided when you are working with 2 tables." });
                }

                if (profile.OperationType != OperationType.Delete && (!profile.FieldsMapping.Any(a => a.MappingType == MappingType.MergeField || a.MappingType == MappingType.MergeFieldWithCondition || a.MappingType == MappingType.UpdateValue || a.MappingType == MappingType.UpdateValueWithCondition)))
                {
                    ValidationMessages.Add(new() { Message = "At least one type of mapping you need to provide in order to update the record." });
                }
            }
            else
            {
                if (profile.OperationType != OperationType.Delete && (!profile.FieldsMapping.Any(a => a.MappingType == MappingType.UpdateValue || a.MappingType == MappingType.UpdateValueWithCondition)))
                {
                    ValidationMessages.Add(new() { Message = "At least one type of mapping you need to provide in order to update the record." });
                }
            }
        }

        public List<ValidationMessage> ValidationMessages { get; set; } = new();
    }

    public class ValidationMessage
    {
        public string Message { get; set; }
    }

    public class ValidationMessageEventArgs : EventArgs
    {
        public readonly List<ValidationMessage> ValidationMessages;

        public ValidationMessageEventArgs(List<ValidationMessage> validationMessages)
        {
            ValidationMessages = validationMessages;
        }
    }

    public class ValidationMessagePublisher : Publisher<List<ValidationMessage>, ValidationMessageEventArgs>
    {

    }
}
