using Migration.Repository.LogModels;

namespace Migration.Repository.Delegates
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