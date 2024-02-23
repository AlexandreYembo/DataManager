using Newtonsoft.Json;

namespace Migration.Models.Logs
{
    public class LogDetails
    {
        public int JobId { get; set; }
        public string Title { get; set; }
        public LogType Type { get; set; }
        public bool Display { get; set; }
        public List<string> Descriptions { get; set; } = new();

        public List<ActionsLog>? ActionsLogs { get; set; } = new();
        public OperationType OperationType { get; set; }
        public DateTime LogDateTime { get; set; }
    }

    public class ActionsLog
    {
        public string Label { get; set; }

        [JsonIgnore]
        public Func<Task>? Action { get; set; }
    }
}