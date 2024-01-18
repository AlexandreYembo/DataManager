using Newtonsoft.Json;

namespace Migration.Repository.LogModels
{
    public class LogDetails
    {
        public int JobId { get; set; }
        public string Title { get; set; }
        public LogType Type { get; set; }
        public bool Display { get; set; }
        public List<string> Descriptions { get; set; } = new();
        public List<ActionsLog> ActionsLogs { get; set; } = new();
    }

    public class ActionsLog
    {
        public string Label { get; set; }

        [JsonIgnore]
        public Func<Task>? Action { get; set; }
    }
}