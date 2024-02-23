using Connectors.Redis;
using Connectors.Redis.Models;
using Migration.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Services.Operations
{
    public abstract class OperationBase
    {
        private readonly IRepository<JObject> _migrationProcessRepository;
        private readonly IJobService _jobService;

        public OperationBase(IRepository<JObject> migrationProcessRepository, IJobService jobService)
        {
            _migrationProcessRepository = migrationProcessRepository;
        }

        public async Task SaveCopyInLocal(string entity, JObject data, Jobs job)
        {
            var redisData = new HashKeyRedisData<JObject>()
            {
                RedisValue = $"{data["id"]}",
                Data = data,
                RedisKey = $"{entity}${job.JobCategory}${job.JobId}$"
            };

            await _migrationProcessRepository.SaveAsync(redisData);
        }

        public async Task UpdateJob(Jobs job)
        {
            await _jobService.UpdateJob(job);
        }
    }
}