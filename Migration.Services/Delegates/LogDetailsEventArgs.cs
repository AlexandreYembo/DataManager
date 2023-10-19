using Migration.Services.LogModels;

namespace Migration.Services.Delegates
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