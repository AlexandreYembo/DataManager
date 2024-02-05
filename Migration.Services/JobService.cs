using Migration.Infrastructure.Redis;
using Migration.Infrastructure.Redis.Entities;
using Migration.Repository.Models;
using Migration.Repository.Publishers;
using Newtonsoft.Json;

namespace Migration.Services
{
    public class JobService : IJobService
    {
        private readonly IRepository<Jobs> _jobRepository;
        private readonly JobsPublisher _jobPublisher;

        public JobService(IRepository<Jobs> jobRepository, JobsPublisher jobPublisher)
        {
            _jobRepository = jobRepository;
            _jobPublisher = jobPublisher;
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
                var jobs = await _jobRepository.CountAsync(nameof(Jobs));
                jobId = jobs + 1;

                var job = NewObject(profile, JobStatus.InProgress, jobId);

                await _jobRepository.SaveAsync(new RedisData<Jobs>()
                {
                    Data = job,
                    RedisValue= jobId.ToString(),
                });

                return job;
            }
        }

        private static Jobs NewObject(Profile profile, JobStatus status, int jobId)
        {
            var job = new Jobs();

            job.JobId = jobId;
            job.ProfileId = profile.DataMappings[0].Id;
            job.OperationType = profile.DataMappings[0].OperationType;
            job.Status = status;
            job.SourceProcessed = 0;
            job.DestinationProcessed = 0;
            job.JobCategory = profile.JobCategoryId;

            return job;
        }

        public async Task UpdateJob(Jobs job)
        {
            await _jobRepository.SaveAsync(new RedisData<Jobs>()
            {
                Data = job,
                RedisValue = job.JobId.ToString(),
            });

            await _jobPublisher.PublishAsync(job);
        }

        public async Task CreateAndAddToTheQueue(Profile profile)
        {
            var jobs = await _jobRepository.CountAsync(nameof(Jobs));
            var jobId = jobs + 1;

            var job = NewObject(profile, JobStatus.Queued, jobId);

            await _jobRepository.SaveAsync(new RedisData<Jobs>()
            {
                Data = job,
                RedisValue = jobId.ToString(),
            });
        }
    }
}