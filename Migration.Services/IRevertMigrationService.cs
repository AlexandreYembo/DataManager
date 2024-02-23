using Migration.Models.Profile;
using Newtonsoft.Json.Linq;

namespace Migration.Services
{
    public interface IRevertMigrationService
    {
        Task Revert(ProfileConfiguration profile, List<JObject> listData, int jobId);
    }
}
