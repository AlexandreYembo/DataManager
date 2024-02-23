using Migration.EventHandlers.CustomEventArgs;
using Migration.Models.Logs;

namespace Migration.EventHandlers.Publishers
{
    public class LogDetailsPublisher : Publisher<LogDetails, LogDetailsEventArgs>
    {
    }
}