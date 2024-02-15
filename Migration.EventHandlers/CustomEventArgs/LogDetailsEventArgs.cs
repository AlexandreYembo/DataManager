using Migration.Models.Logs;

namespace Migration.EventHandlers.CustomEventArgs
{
    public class LogDetailsEventArgs : EventArgs
    {
        public LogDetails LogDetail { get; set; }
        public LogDetailsEventArgs(LogDetails logDetail)
        {
            LogDetail = logDetail;
        }
    }
}