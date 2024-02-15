namespace Migration.Models.Profile
{
    public class SearchCondition
    {
        public SearchConditionType? Type { get; set; }
        public string Query { get; set; }
        public MappingDirectionType ConditionDirection { get; set; }
    }
}