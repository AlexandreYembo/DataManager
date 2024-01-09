using Migration.Repository.Delegates;
using Migration.Repository.LogModels;

namespace Migration.Repository.Subscribers
{
    public class LogResultSubscriber
    {
        public LogResult LogResult = new();

        public void OnEventChanged(object source, LogResultEventArgs args)
        {
            LogResult = args.LogResult;
        }

        public void OnEventChanged(object source, LogDetailsEventArgs args)
        {
            LogResult.Details.Add(args.LogDetail);
        }
    }
}