using Migration.Repository.Models;

namespace Migration.Services
{
    public interface IJobService
    {
        Task<Jobs> GetOrCreateJob(Profile profile, int jobId);
        Task UpdateJob(Jobs job);
    }
}