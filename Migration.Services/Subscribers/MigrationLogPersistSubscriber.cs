using Migration.Infrastructure.Redis.Entities;
using Migration.Infrastructure.Redis;
using Migration.Repository.LogModels;
using Newtonsoft.Json;

namespace Migration.Services.Subscribers
{
    public class MigrationLogPersistSubscriber
    {
        private readonly IRepository<LogResult> _logRepository;

        public MigrationLogPersistSubscriber(IRepository<LogResult> logRepository)
        {
            _logRepository = logRepository;
        }

        public void LogResultPublisher_OnEntityChanged(object? sender, Repository.Delegates.LogResultEventArgs e)
        {
            var redisValue = _logRepository.FindByKeyAsync(new RedisData<LogResult>()
            {
                RedisValue = "Job:" + e.LogResult.JobId + "_Operation:" + e.LogResult.OperationType
            }).GetAwaiter().GetResult();

            if (redisValue.HasValue)
            {
                var log = JsonConvert.DeserializeObject<LogResult>(redisValue);

                log.FinishedIn = DateTime.Now;
                log.TotalRecords = e.LogResult.TotalRecords;

                _logRepository.SaveAsync(new RedisData<LogResult>()
                {
                    Data = log,
                    RedisValue = "Job:" + e.LogResult.JobId + "_Operation:" + e.LogResult.OperationType
                }).GetAwaiter().GetResult();
            }
            else
            {
                _logRepository.SaveAsync(new RedisData<LogResult>()
                {
                    Data = e.LogResult,
                    RedisValue = "Job:" + e.LogResult.JobId + "_Operation:" + e.LogResult.OperationType
                }).GetAwaiter().GetResult();
            }
        }

        public void LogDetailsPublisher_OnEntityChanged(object? sender, Repository.Delegates.LogDetailsEventArgs e)
        {
            e.LogDetail.LogDateTime = DateTime.Now;

            var redisValue = _logRepository.FindByKeyAsync(new RedisData<LogResult>()
            {
                RedisValue = "Job:" + e.LogDetail.JobId + "_Operation:" + e.LogDetail.OperationType
            }).GetAwaiter().GetResult();

            var log = JsonConvert.DeserializeObject<LogResult>(redisValue.ToString());

            if (log == null) return;

            if ((!log.Details.Any() ||
                log.Details.FirstOrDefault(w => w.Title == e.LogDetail.Title) == null) ||
                !log.Details.FirstOrDefault(w => w.Title == e.LogDetail.Title).Descriptions.Any())
            {
                var details = new LogDetails()
                {
                    LogDateTime = e.LogDetail.LogDateTime,
                    OperationType = e.LogDetail.OperationType,
                    Descriptions = e.LogDetail.Descriptions,
                    Display = e.LogDetail.Display,
                    JobId = e.LogDetail.JobId,
                    Title = e.LogDetail.Title,
                    Type = e.LogDetail.Type
                };

                log.Details.Add(details);
            }
            else
            {
                var descriptions = log.Details.FirstOrDefault(w => w.Title == e.LogDetail.Title).Descriptions;

                descriptions.AddRange(e.LogDetail.Descriptions);

                log.Details.FirstOrDefault(w => w.Title == e.LogDetail.Title).Descriptions = descriptions;
                log.Details.FirstOrDefault(w => w.Title == e.LogDetail.Title).LogDateTime = DateTime.Now;
            }

            _logRepository.SaveAsync(new RedisData<LogResult>()
            {
                Data = log,
                RedisValue = "Job:" + e.LogDetail.JobId + "_Operation:" + e.LogDetail.OperationType
            }).GetAwaiter().GetResult();
        }
    }
}