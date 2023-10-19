using Migration.Repository.DbOperations;

namespace Migration.Repository.Models
{
    public class DataMapping
    {
        public AggregateSource Source { get; set; }
        public AggregateDestination Destination { get; set; }
        public List<CommandModel> Commands { get; set; } = new();
        public List<DataFieldsMapping> FieldsMapping { get; set; } = new();
        public OperationType OperationType { get; set; }

        public int Id { get; set; }
    }

    /// <summary>
    /// Has configuration for the Source of Data (Query)
    /// </summary>
    public class AggregateSource
    {
        public DBSettings DBSettings { get; set; }
        public string Query { get; set; }
    }

    /// <summary>
    /// Has configuration for the Destination of Data (Insert/Update)
    /// </summary>
    public class AggregateDestination
    {
        public DBSettings DBSettings { get; set; }
        public string Query { get; set; }
    }

    public class DataFieldsMapping
    {
        public string SourceField { get; set; }
        public string DestinationField { get; set; }
    }

    public enum OperationType
    {
        Insert,
        Update
    }
}