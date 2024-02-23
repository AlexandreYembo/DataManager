using Migration.Core;
using Migration.Models;
using Migration.Models.Profile;
using Newtonsoft.Json.Linq;

namespace Migration.Services.Operations
{
    public interface IOperation
    {
        Task ProcessDataAsync(IGenericRepository repository, List<(EntityType entityType, string id, JObject data)> data, ProfileConfiguration profile, Jobs job);
    }
}