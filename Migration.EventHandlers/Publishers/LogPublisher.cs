using Migration.EventHandlers.CustomEventArgs;
using Migration.Models.Logs;

namespace Migration.EventHandlers.Publishers
{
    public class LogPublisher : Publisher<LogResult, LogResultEventArgs>
    {
    }
}