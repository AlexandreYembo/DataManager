using Migration.Repository.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Services
{
    public interface IRevertMigrationService
    {
        Task Revert(Profile profile, List<JObject> listData, int jobId);
    }
}
