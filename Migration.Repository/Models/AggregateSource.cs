using Migration.Repository.DbOperations;

namespace Migration.Repository.Models
{
    public class Profile
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public DataMapping DataMapping { get; set; } = new();
    }

    public class DataMapping
    {
        public List<AggregateData> Source { get; set; } = new();
        public List<AggregateData> Destination { get; set; } = new();
        public List<CommandModel> Commands { get; set; } = new();
        public List<DataFieldsMapping> FieldsMapping { get; set; } = new();
    }

    /// <summary>
    /// Has configuration for the Source of Data (Query)
    /// </summary>
    public class AggregateData
    {
        public DataSettings Settings { get; set; } = new();
        public string? Query { get; set; }
        public OperationType OperationType { get; set; }
    }

    public class DataFieldsMapping
    {
        public string SourceEntity { get; set; }
        public string DestinationEntity { get; set; }
        public MappingType MappingType { get; set; }
        public OperatorType? OperatorType { get; set; }
        public JoinType? JoinType { get; set; }
        public string? SourceField { get; set; }
        public string? DestinationField { get; set; }
        public FieldValueType? ValueType { get; set; }
        public List<SearchCondition> SourceCondition { get; set; } = new();
        public string? ValueField { get; set; }
    }

    public class SearchCondition
    {
        public SearchConditionType? Type { get; set; }
        public string Query { get; set; }
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
        FieldValueMerge,
        ValueWithCondition,
        FieldValueMergeWithCondition
    }

    public enum OperationType
    {
        Insert,
        Update,
    }

    public enum OperatorType
    {
        ArrayContains,
        In,
        Eq,
        EqAnd,
        EqOr,
    }

    public enum JoinType
    {
       BetweenSource,
       Destination
    }

    public enum SearchConditionType
    {
        And,
        Or
    }
}