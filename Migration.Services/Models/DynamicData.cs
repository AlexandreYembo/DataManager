
namespace Migration.Services.Models
{
    public class DynamicData
    {
        public string Id { get; set; }
        public string Data { get; set; }
        public DataType DataType { get; set; }
        public List<ActionType> Actions{ get; set; } = new () { ActionType.None };
    }
}