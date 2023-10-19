using Migration.Services.LogModels;

namespace Migration.Services.Delegates
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