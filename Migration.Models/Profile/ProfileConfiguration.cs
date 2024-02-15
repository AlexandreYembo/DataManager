namespace Migration.Models.Profile
{
    public class ProfileConfiguration
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? JobCategoryId { get; set; }
        public Configuration Source { get; set; } = new();
        public Configuration Target { get; set; } = new();
        public List<DataFieldsMapping> FieldsMapping { get; set; } = new();
        public OperationType OperationType { get; set; }
        public string Id { get; set; }
        public DataQueryMappingType DataQueryMappingType { get; set; }
    }
}