using Migration.Repository.LogModels;

namespace Migration.Repository.Delegates
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