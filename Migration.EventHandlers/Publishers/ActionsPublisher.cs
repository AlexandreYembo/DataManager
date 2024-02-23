using Migration.EventHandlers.CustomEventArgs;
using Migration.Models;

namespace Migration.EventHandlers.Publishers
{
    public class ActionsPublisher : Publisher<Actions, ActionsEventArgs>
    {
    }
}