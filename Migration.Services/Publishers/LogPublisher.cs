
using Migration.Services.Delegates;
using Migration.Services.LogModels;

namespace Migration.Services.Publishers
{
    public class LogPublisher : Publisher<LogResult, LogResultEventArgs>
    {
    }

    public class LogDetailsPublisher : Publisher<LogDetails, LogDetailsEventArgs>
    {
    }
}