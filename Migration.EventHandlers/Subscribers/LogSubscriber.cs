using Migration.EventHandlers.CustomEventArgs;
using Migration.Models.Logs;

namespace Migration.EventHandlers.Subscribers
{
    public class LogSubscriber
    {
        public LogResult LogResult = new();

        public void OnEventChanged(object source, LogResultEventArgs args)
        {
            LogResult = args.LogResult;
        }

        public void OnEventChanged(object source, LogDetailsEventArgs args)
        {
            if (LogResult.Details == null)
                LogResult.Details = new();

            args.LogDetail.LogDateTime = DateTime.Now;

            var temp = LogResult.Details.ToArray();

            bool addNew = true;
            foreach (var item in temp)
            {
                if (item.Title == args.LogDetail.Title)
                {
                    if (args.LogDetail.Descriptions != null)
                    {
                        item.Descriptions.AddRange(args.LogDetail.Descriptions);
                        item.ActionsLogs = args.LogDetail.ActionsLogs;
                        item.LogDateTime = args.LogDetail.LogDateTime;
                    }

                    addNew = false;
                }
            }

            if (!addNew)
                LogResult.Details = temp.ToList();
            else
                LogResult.Details.Add(args.LogDetail);
        }
    }
}