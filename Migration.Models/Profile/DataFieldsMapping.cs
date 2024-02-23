namespace Migration.Models.Profile
{
    public class DataFieldsMapping
    {
        public string SourceEntity { get; set; }
        public string TargetEntity { get; set; }
        public SearchConditionType? JoinType { get; set; }
        public MappingType? MappingType { get; set; }
        public OperatorType? OperatorType { get; set; }
        public bool IgnoreCaseSensitive { get; set; }
        public string? SourceField { get; set; }
        public string? TargetField { get; set; }
        public FieldValueType? ValueType { get; set; }
        public List<SearchCondition> Conditions { get; set; } = new();
        public string? ValueField { get; set; }
    }
}