namespace Migration.Repository.LogModels
{
    public class LogResult
    {
        public int JobId { get; set; }
        public DateTime StartedIn { get; set; }
        public DateTime FinishedIn { get; set; }

        public string TimeConsumed => GetTimeConsume();
        public int TotalRecords { get; set; }
        public int TotalSuccess => Details.Count(d => d.Type == LogType.Success && d.Display);
        public int TotalWarns => Details.Count(d => d.Type == LogType.Warn && d.Display);
        public int TotalFailed => Details.Count(d => d.Type == LogType.Error && d.Display);
        public string EntityName { get; set; }
        public string Description { get; set; }
        public List<LogDetails> Details { get; set; } = new();

        private string GetTimeConsume()
        {
            if (StartedIn == default || FinishedIn == default) return string.Empty;

            TimeSpan ts = FinishedIn - StartedIn;
            return ToReadableString(ts);
        }

        public static string ToReadableString(TimeSpan span)
        {
            string formatted = string.Format("{0}{1}{2}{3}",
                span.Duration().Days > 0
                    ? string.Format("{0:0} day{1}, ", span.Days, span.Days == 1 ? string.Empty : "s")
                    : string.Empty,
                span.Duration().Hours > 0
                    ? string.Format("{0:0} hour{1}, ", span.Hours, span.Hours == 1 ? string.Empty : "s")
                    : string.Empty,
                span.Duration().Minutes > 0
                    ? string.Format("{0:0} minute{1}, ", span.Minutes, span.Minutes == 1 ? string.Empty : "s")
                    : string.Empty,
                span.Duration().Seconds > 0
                    ? string.Format("{0:0} second{1}", span.Seconds, span.Seconds == 1 ? string.Empty : "s")
                    : string.Empty);

            if (formatted.EndsWith(", ")) formatted = formatted.Substring(0, formatted.Length - 2);

            if (string.IsNullOrEmpty(formatted)) formatted = "0 seconds";

            return formatted;
        }
    }
    public enum LogType
    {
        Success,
        Warn,
        Error
    }
}