using Migration.EventHandlers.CustomEventArgs;
using Migration.Models.Logs;

namespace Migration.EventHandlers.Publishers
{
    public class GetLogPublisher : Publisher<LogResult, LogResultEventArgs>
    {
    }
}