using Migration.Infrastructure.Redis.Entities;
using Migration.Infrastructure.Redis;
using Migration.Repository.LogModels;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Migration.Services.Subscribers
{
    public class MigrationLogPersistSubscriber
    {
        private readonly IRepository<JObject> _migrationProcessRepository;

        public MigrationLogPersistSubscriber(IRepository<JObject> migrationProcessRepository)
        {
            _migrationProcessRepository = migrationProcessRepository;
        }

        public void LogResultPublisher_OnEntityChanged(object? sender, Repository.Delegates.LogResultEventArgs e)
        {
            Task.Run(() =>
            {
                _migrationProcessRepository.SaveAsync(new RedisData<JObject>()
                {
                    Data = JObject.FromObject(e.LogResult),
                    RedisValue = e.LogResult.JobId.ToString(),
                    RedisKey = "Logs"
                });
            });
        }

        public void LogDetailsPublisher_OnEntityChanged(object? sender, Repository.Delegates.LogDetailsEventArgs e)
        {
            Task.Run(async () =>
            {
                var redisValue = await _migrationProcessRepository.FindByKeyAsync(new RedisData<JObject>()
                {
                    RedisKey = "Logs",
                    RedisValue = e.LogDetail.JobId.ToString()
                });

                var log = JsonConvert.DeserializeObject<LogResult>(redisValue.ToString());

                if (!log.Details.Any() ||
                    log.Details.FirstOrDefault(w => w.Title == e.LogDetail.Title) == null ||
                    !log.Details.FirstOrDefault(w => w.Title == e.LogDetail.Title).Descriptions.Any())
                {
                    log.Details.Add(e.LogDetail);
                }
                else
                {
                    var descriptions = log.Details.FirstOrDefault(w => w.Title == e.LogDetail.Title).Descriptions;

                    descriptions.AddRange(e.LogDetail.Descriptions);

                    log.Details.FirstOrDefault(w => w.Title == e.LogDetail.Title).Descriptions = descriptions;
                }

                await _migrationProcessRepository.SaveAsync(new RedisData<JObject>()
                {
                    Data = JObject.FromObject(log),
                    RedisValue = e.LogDetail.JobId.ToString(),
                    RedisKey = "Logs"
                });
            });
        }
    }
}
