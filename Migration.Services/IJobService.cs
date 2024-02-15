using Migration.Models;
using Migration.Models.Profile;

namespace Migration.Services
{
    public interface IJobService
    {
        Task<Jobs> GetOrCreateJob(ProfileConfiguration profile, int jobId);
        Task UpdateJob(Jobs job);
        Task CreateAndAddToTheQueue(ProfileConfiguration profile);
    }
}