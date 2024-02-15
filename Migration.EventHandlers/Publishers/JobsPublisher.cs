using Migration.EventHandlers.CustomEventArgs;
using Migration.Models;

namespace Migration.EventHandlers.Publishers
{
    public class JobsPublisher : Publisher<Jobs, JobsEventArgs>
    {
    }
}
