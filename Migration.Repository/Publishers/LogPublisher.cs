
using Migration.Repository.Delegates;
using Migration.Repository.LogModels;

namespace Migration.Repository.Publishers
{
    public class LogPublisher : Publisher<LogResult, LogResultEventArgs>
    {
    }

    public class LogDetailsPublisher : Publisher<LogDetails, LogDetailsEventArgs>
    {
    }
}