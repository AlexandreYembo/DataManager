using Migration.Repository.DbOperations;

namespace Migration.Repository.Models
{
    public class Profile
    {
        public string Name{ get; set; }
        public string Description { get; set; }
        public List<DataMapping> DataMappings { get; set; } = new();
    }

    public class DataMapping
    {
        public AggregateData Source { get; set; }
        public AggregateData Destination { get; set; }
        public List<CommandModel> Commands { get; set; } = new();
        public List<DataFieldsMapping> FieldsMapping { get; set; } = new();
        public OperationType OperationType { get; set; }

        public int Id { get; set; }
    }

    /// <summary>
    /// Has configuration for the Source of Data (Query)
    /// </summary>
    public class AggregateData
    {
        public DataSettings Settings { get; set; } = new();
        public string Query { get; set; }
    }

    public class DataFieldsMapping
    {
        public MappingType MappingType { get; set; }
        public OperatorType? OperatorType { get; set; }
        public string SourceField { get; set; }
        public string DestinationField { get; set; }
    }

    public enum OperationType
    {
        Insert,
        Update
    }

    public enum MappingType
    {
        tableJoin,
        valueMerge,
        condition
    }

    public enum OperatorType
    {
        ArrayContains,
        In,
        Eq,
    }
}