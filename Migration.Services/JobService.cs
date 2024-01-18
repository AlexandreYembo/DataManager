using Migration.Infrastructure.Redis;
using Migration.Infrastructure.Redis.Entities;
using Migration.Repository.Models;
using Newtonsoft.Json;

namespace Migration.Services
{
    public class JobService : IJobService
    {
        private readonly IRepository<Jobs> _jobRepository;

        public JobService(IRepository<Jobs> jobRepository)
        {
            _jobRepository = jobRepository;
        }

        public async Task<Jobs> GetOrCreateJob(Profile profile, int jobId)
        {
            if (jobId > 0)
            {
                var redisValue = await _jobRepository.FindByKeyAsync(new RedisData<Jobs>()
                {
                    RedisValue = jobId.ToString()
                });

                var job = JsonConvert.DeserializeObject<Jobs>(redisValue);

                return job;
            }
            else
            {
                var job = new Jobs();
                var jobs = await _jobRepository.CountAsync(nameof(Jobs));
                jobId = jobs + 1;

                job.JobId =  jobId;
                job.ProfileId = profile.DataMappings[0].Id;
                job.OperationType = profile.DataMappings[0].OperationType;
                job.Status = "In Progress";
                job.SourceProcessed = 0;
                job.DestinationProcessed = 0;
                job.JobCategory= profile.JobCategoryId;

                await _jobRepository.SaveAsync(new RedisData<Jobs>()
                {
                    Data = job,
                    RedisValue= jobId.ToString(),
                });

                return job;
            }
        }

        public async Task UpdateJob(Jobs job)
        {
            await _jobRepository.SaveAsync(new RedisData<Jobs>()
            {
                Data = job,
                RedisValue = job.JobId.ToString(),
            });
        }
    }
}