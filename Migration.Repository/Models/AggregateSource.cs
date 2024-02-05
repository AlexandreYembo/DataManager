using Migration.Repository.DbOperations;

namespace Migration.Repository.Models
{
    public class Profile
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? JobCategoryId { get; set; }
        public List<DataMapping> DataMappings { get; set; } = new();
        public bool Selected { get; set; }
    }

    public class DataMapping
    {
        public AggregateData Source { get; set; } = new();
        public AggregateData Destination { get; set; } = new();
        public List<CommandModel> Commands { get; set; } = new();
        public List<DataFieldsMapping> FieldsMapping { get; set; } = new();
        public OperationType OperationType { get; set; }
        
        public string Id { get; set; }
        public DataQueryMappingType DataQueryMappingType { get; set; }
    }

    /// <summary>
    /// Has configuration for the Source of Data (Query)
    /// </summary>
    public class AggregateData
    {
        public DataSettings Settings { get; set; } = new();
        public string? Query { get; set; }
    }

    public class DataFieldsMapping
    {
        public string SourceEntity { get; set; }
        public string DestinationEntity { get; set; }
        public MappingType? MappingType { get; set; }
        public OperatorType? OperatorType { get; set; }
        public bool IgnoreCaseSensitive { get; set; }
        public string? SourceField { get; set; }
        public string? DestinationField { get; set; }
        public FieldValueType? ValueType { get; set; }
        public List<SearchCondition> Conditions { get; set; } = new();
        public string? ValueField { get; set; }
    }

    public class SearchCondition
    {
        public SearchConditionType? Type { get; set; }
        public string Query { get; set; }
        public MappingDirectionType ConditionDirection { get; set; }
    }


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

    public enum MappingDirectionType
    {
        Source,
        Destination
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

    public enum OperationType
    {
        Import,
        Update,
        Delete,
        Report
    }

    public enum DataQueryMappingType
    {
        UpdateSameCollection,
        UpdateAnotherCollection
    }
}