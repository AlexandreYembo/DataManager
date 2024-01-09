using Newtonsoft.Json.Linq;

namespace Migration.BackgroundWorker.Services
{
    public interface IMigrationService
    {
        Task Process(string jobId);
    }
}