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
            args.LogDetail.LogDateTime = DateTime.Now;

            if (!LogResult.Details.Any() || !LogResult.Details.Any(w => w.Title == args.LogDetail.Title))
            {
                LogResult.Details.Add(args.LogDetail);
            }
            else
            {
                LogResult.Details.FirstOrDefault(w => w.Title == args.LogDetail.Title).Descriptions.AddRange(args.LogDetail.Descriptions);
                LogResult.Details.FirstOrDefault(w => w.Title == args.LogDetail.Title).ActionsLogs = args.LogDetail.ActionsLogs;
                LogResult.Details.FirstOrDefault(w => w.Title == args.LogDetail.Title).LogDateTime = args.LogDetail.LogDateTime;
            }

        }
    }
}