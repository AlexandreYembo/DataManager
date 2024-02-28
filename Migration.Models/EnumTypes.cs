namespace Migration.Models
{
    public enum FieldValueType
    {
        String,
        Integer,
        Boolean,
        Guid,
        DateTime
    }

    public enum MappingType
    {
        TableJoin,
        UpdateValue,
        MergeField,
        UpdateValueWithCondition,
        MergeFieldWithCondition
    }

    public enum OperationType
    {
        Import,
        Update,
        Delete,
        CacheData,
        Report
    }

    public enum OperatorType
    {
        ArrayContains,
        In,
        Eq,
    }

    public enum SearchConditionType
    {
        And,
        Or
    }

    public enum DataQueryMappingType
    {
        SameCollection,
        SourceToTarget
    }

    public enum MappingDirectionType
    {
        Source,
        Target
    }

    public enum ActionEventType
    {
        Success,
        Error
    }
    public enum JobStatus
    {
        InProgress,
        Queued,
        Waiting,
        Completed,
        Error
    }

    public enum TableAttributesType
    {
        PartitionKey,
        RowKey,
        RecordId
    }

    public enum EntityType
    {
        Source,
        Target,
        TargetCache
    }
}