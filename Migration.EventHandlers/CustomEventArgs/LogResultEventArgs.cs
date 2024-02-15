using Migration.Models.Logs;

namespace Migration.EventHandlers.CustomEventArgs
{
    public class LogResultEventArgs : EventArgs
    {
        public LogResult LogResult { get; set; }

        public LogResultEventArgs(LogResult logResult)
        {
            LogResult = logResult;
        }
    }
}
