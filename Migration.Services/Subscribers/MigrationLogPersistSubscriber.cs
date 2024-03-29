﻿using Newtonsoft.Json;
using Migration.EventHandlers.CustomEventArgs;
using Migration.Models.Logs;
using Connectors.Redis;
using Connectors.Redis.Models;

namespace Migration.Services.Subscribers
{
    public class MigrationLogPersistSubscriber
    {
        private readonly IRepository<LogResult> _logRepository;

        public MigrationLogPersistSubscriber(IRepository<LogResult> logRepository)
        {
            _logRepository = logRepository;
        }

        public void LogResultPublisher_OnEntityChanged(object? sender, LogResultEventArgs e)
        {
            var redisValue = _logRepository.FindByKeyAsync(new HashKeyRedisData<LogResult>()
            {
                RedisValue = "Job:" + e.LogResult.JobId + "_Operation:" + e.LogResult.OperationType
            }).GetAwaiter().GetResult();

            if (redisValue.HasValue)
            {
                var log = JsonConvert.DeserializeObject<LogResult>(redisValue);

                log.FinishedIn = DateTime.Now;

                log.Details = e.LogResult.Details;
                log.TotalRecords = e.LogResult.TotalRecords;

                _logRepository.SaveAsync(new HashKeyRedisData<LogResult>()
                {
                    Data = log,
                    RedisValue = "Job:" + e.LogResult.JobId + "_Operation:" + e.LogResult.OperationType
                }).GetAwaiter().GetResult();
            }
            else
            {
                _logRepository.SaveAsync(new HashKeyRedisData<LogResult>()
                {
                    Data = e.LogResult,
                    RedisValue = "Job:" + e.LogResult.JobId + "_Operation:" + e.LogResult.OperationType
                }).GetAwaiter().GetResult();
            }
        }
    }
}