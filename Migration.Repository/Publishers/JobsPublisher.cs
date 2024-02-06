using Migration.Repository.Delegates;
using Migration.Repository.Models;

namespace Migration.Repository.Publishers
{
    public class JobsPublisher : Publisher<Jobs, JobsEventArgs>
    {
    }
}