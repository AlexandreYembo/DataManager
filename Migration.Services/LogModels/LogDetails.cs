namespace Migration.Services.LogModels
{
    public class LogDetails
    {
        public string Title { get; set; }
        public List<string> Details { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public LogType Type { get; set; }
        public bool Display { get; set; }
        public bool IsDataBackup { get; set; }

        // new feature to review and do an action individually
        public string Actions { get; set; }
    }
}