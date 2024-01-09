using Migration.Infrastructure.Redis;
using Migration.Infrastructure.Redis.Entities;
using Migration.Repository;
using Migration.Repository.Exceptions;
using Migration.Repository.Extensions;
using Migration.Repository.LogModels;
using Migration.Repository.Models;
using Migration.Repository.Publishers;
using Migration.Services.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Migration.Services
{
    public interface IMigrationService
    {
        Task Migrate(DataMapping dataMapping, string environment, int jobId);
    }

    public class MigrationService : IMigrationService
    {
        private readonly Func<DataSettings, IGenericRepository> _genericRepository;
        private readonly IRepository<JObject> _migrationProcessRepository;
        private readonly LogPublisher _logResultPublisher;
        private readonly LogDetailsPublisher _logDetailsPublisher;

        public MigrationService(Func<DataSettings, IGenericRepository> genericRepository,
            IRepository<JObject> migrationProcessRepository,
            LogPublisher logPublisher,
            LogDetailsPublisher logDetailsPublisher)
        {
            _genericRepository = genericRepository;
            _migrationProcessRepository = migrationProcessRepository;
            _logResultPublisher = logPublisher;
            _logDetailsPublisher = logDetailsPublisher;

            _logResultPublisher.OnEntityChanged += LogResultPublisher_OnEntityChanged;
            _logDetailsPublisher.OnEntityChanged += LogDetailsPublisher_OnEntityChanged;
        }

        public async Task Migrate(DataMapping dataMapping, string environment, int jobId)
        {
            List<Task> processTasks = new();
            var job = await GetOrCreateJob(dataMapping, environment, jobId);

            LogResult log = new()
            {
                EntityName = dataMapping.DataQueryMappingType == DataQueryMappingType.UpdateAnotherCollection ? dataMapping.Destination.Settings.CurrentEntity
                    : dataMapping.Source.Settings.CurrentEntity,
                StartedIn = DateTime.Now,
                Description = $"Processing migration for Operation: {dataMapping.OperationType}.",
                JobId = job["JobId"].ToString()
            };

            _logResultPublisher.Publish(log);

            bool hasRecord;
            int skip = int.Parse(job["SourceProcessed"].ToString());
            int take = 15;

            do
            {
                var repository = _genericRepository(dataMapping.Source.Settings);
                var source = await repository.Get(dataMapping.Source.Query, null, null, take, skip);
                log.TotalRecords += source.Count;

                if (source.Any())
                {
                    if (dataMapping.DataQueryMappingType == DataQueryMappingType.UpdateAnotherCollection)
                    {
                        foreach (var sourceData in source.Where(w => w.Value != "{}" || w.Value != "null"))
                        {
                            await ProcessDestinationRecordsAsync(dataMapping, sourceData, job);
                        }

                        skip = int.Parse(job["SourceProcessed"].ToString());
                        //TODO: Subscribe an event to add the records already processed, can save pagination OFFSET <offset_amount> LIMIT <limit_amount>
                        //https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query/offset-limit
                    }
                    else
                    {
                        foreach (var sourceData in source)
                        {
                            processTasks.Add(ProcessSourceRecordsAsync(repository, dataMapping, sourceData, job));
                        }

                        await Task.WhenAll(processTasks);

                        skip = int.Parse(job["SourceProcessed"].ToString());

                        skip += take;
                        job["SourceProcessed"] = skip;
                        await UpdateJob(job);
                    }
                    hasRecord = true;
                }
                else
                {
                    hasRecord = false;
                }
            } while (hasRecord);


            log.FinishedIn = DateTime.Now;
            _logResultPublisher.Publish(log);
        }

        private async Task<JObject> GetOrCreateJob(DataMapping dataMapping, string environment, long jobId)
        {
            if (jobId > 0)
            {
                var redisValue = await _migrationProcessRepository.FindByKeyAsync(new RedisData<JObject>()
                {
                    Id = "Jobs",
                    Key = jobId.ToString()
                });

                var job = JObject.Parse(redisValue.ToString());

                return job;
            }
            else
            {
                var job = new JObject();
                var jobs = await _migrationProcessRepository.CountAsync("Jobs");
                jobId = jobs + 1;

                job.Add("JobId", jobId);
                job.Add("ProfileId", dataMapping.Id);
                job.Add("OperationType", dataMapping.OperationType.ToString());
                job.Add("Status", "In Progress");
                job.Add("SourceProcessed", 0);
                job.Add("DestinationProcessed", 0);
                job.Add("Environment", environment);

                await _migrationProcessRepository.SaveAsync(new RedisData<JObject>()
                {
                    Data = job,
                    Key = jobId.ToString(),
                    Id = jobId.ToString()
                }, "Jobs");
                return job;
            }
        }

        private async Task UpdateJob(JObject jObject)
        {
            await _migrationProcessRepository.SaveAsync(new RedisData<JObject>()
            {
                Data = jObject,
                Key = jObject["JobId"].ToString(),
                Id = jObject["JobId"].ToString(),
            }, "Jobs");
        }

        private async Task ProcessSourceRecordsAsync(IGenericRepository repository, DataMapping dataMapping,
            KeyValuePair<string, string> data, JObject job)
        {
            var originalData = JObject.Parse(data.Value);
            var jobId = job["JobId"].ToString();
            LogDetails logDetails = new()
            {
                Display = true,
                Title = data.Key,
                JobId = jobId
            };

            try
            {
                if (dataMapping.OperationType == OperationType.Update)
                {
                    Task.Run(async () => await SaveCopyInLocal(dataMapping.Source.Settings.CurrentEntity, JObject.Parse(data.Value), "backup", job));
                    logDetails.Details.Add("Creating copy of the original data");

                    bool hasChange = false;

                    var objectToBeUpdated = UpdateDataHelper.UpdateObject(data.Value, dataMapping.FieldsMapping, ref hasChange);


                    if (!hasChange) return;

                    var differences = DifferenceHelper.FindDifferences(JObject.Parse(data.Value), objectToBeUpdated);

                    if (differences.Any())
                    {
                        logDetails.Details.Add("Values updated: " + string.Join(",", differences.Select(s => s.PropertyName + " = " + s.Object2Value)));
                        await repository.Update(objectToBeUpdated, dataMapping.FieldsMapping);
                    }
                }
                else if (dataMapping.OperationType == OperationType.Report)
                {
                    Task.Run(async () => await SaveCopyInLocal(dataMapping.Source.Settings.CurrentEntity, originalData, "report", job));
                    logDetails.Details.Add("Record exported");
                }
                else if (dataMapping.OperationType == OperationType.Delete)
                {
                    Task.Run(async () => await SaveCopyInLocal(dataMapping.Source.Settings.CurrentEntity, JObject.Parse(data.Value), "backup", job));
                    logDetails.Details.Add("Creating copy of the original data");
                    await repository.Delete(originalData);
                    logDetails.Details.Add("Record deleted");
                }
            }
            catch (DbOperationException e)
            {
                logDetails.Type = LogType.Error;
                logDetails.Errors.Add($"Error: {e.ErrorCode} - {e.ErrorMessage}");
            }
            catch (Exception e)
            {
                logDetails.Type = LogType.Error;
                logDetails.Errors.Add($"Error: {e.Message}");
            }

            _logDetailsPublisher.Publish(logDetails);
        }

        private async Task ProcessDestinationRecordsAsync(DataMapping dataMapping,
            KeyValuePair<string, string> sourceData, JObject job)
        {
            int skip = int.Parse(job["DestinationProcessed"].ToString());
            int take = 15;
            bool hasRecord;

            do
            {
                var repository = _genericRepository(dataMapping.Destination.Settings);
                var destination = await repository.Get(dataMapping.Destination.Query, dataMapping.FieldsMapping, sourceData.Value, take, skip);

                var listDestination = destination.ApplyJoin(sourceData, dataMapping.FieldsMapping);

                hasRecord = listDestination.Any();

                var sourceObj = JObject.Parse(sourceData.Value);
                List<Task> processTasks = new();

                foreach (var originalData in listDestination)
                {
                    processTasks.Add(UpdateDestinationRecordsAsync(dataMapping, originalData, sourceObj, repository, job));
                }

                await Task.WhenAll(processTasks);

                skip += take;
                job["DestinationProcessed"] = skip;
                await UpdateJob(job);
            } while (hasRecord);

            job["DestinationProcessed"] = 0;
            await UpdateJob(job);

            int sourceSkip = int.Parse(job["SourceProcessed"].ToString());

            sourceSkip++;
            job["SourceProcessed"] = sourceSkip;

            await UpdateJob(job);
        }

        private async Task<bool> UpdateDestinationRecordsAsync(DataMapping dataMapping, JObject originalData, JObject sourceObj, IGenericRepository repository, JObject job)
        {
            var jobId = job["JobId"].ToString();
            LogDetails logDetails = new()
            {
                Display = true,
                Title = originalData["id"].ToString(),
                JobId = jobId
            };

            try
            {
                if (dataMapping.OperationType == OperationType.Update)
                {
                    logDetails.Details.Add("Creating copy of the original data");
                    Task.Run(async () =>
                        await SaveCopyInLocal(dataMapping.Destination.Settings.CurrentEntity, originalData, "backup", job));

                    bool hasChange = false;

                    var objectToBeUpdated = UpdateDataHelper.UpdateObject(originalData.ToString(), dataMapping.FieldsMapping, sourceObj, ref hasChange);

                    if (!hasChange) return true;

                    Task.Run(async () =>
                        await SaveCopyInLocal(dataMapping.Destination.Settings.CurrentEntity, objectToBeUpdated, "updated", job));
                    logDetails.Details.Add("Creating copy of the changes");

                    var differences = DifferenceHelper.FindDifferences(originalData, objectToBeUpdated);
                    logDetails.Details.Add("Values updated: " +
                                           string.Join(",", differences.Select(s => s.PropertyName + " = " + s.Object2Value)));

                    await repository.Update(objectToBeUpdated, dataMapping.FieldsMapping);
                }
                else if (dataMapping.OperationType == OperationType.Report)
                {
                    Task.Run(async () => await SaveCopyInLocal(dataMapping.Destination.Settings.CurrentEntity, originalData, "report", job));
                    logDetails.Details.Add("Record exported for analyse");
                }
                else if (dataMapping.OperationType == OperationType.Delete)
                {
                    logDetails.Details.Add("Creating copy of the original data");
                    Task.Run(async () =>
                        await SaveCopyInLocal(dataMapping.Destination.Settings.CurrentEntity, originalData, "backup", job));

                    await repository.Delete(originalData);
                    logDetails.Details.Add("Record deleted");
                }
            }
            catch (DbOperationException e)
            {
                logDetails.Errors.Add($"Error: {e.ErrorCode} - {e.ErrorMessage}");
                logDetails.Type = LogType.Error;
            }

            _logDetailsPublisher.Publish(logDetails);
            return false;
        }

        private async Task SaveCopyInLocal(string entity, JObject data, string suffix, JObject job)
        {
            var redisData = new RedisData<JObject>()
            {
                Key = $"{data["id"]}-{suffix}",
                Data = data
            };

            await _migrationProcessRepository.SaveAsync(redisData, $"{entity}${job["Environment"]}${job["JobId"]}$");
        }

        private void LogResultPublisher_OnEntityChanged(object? sender, Repository.Delegates.LogResultEventArgs e)
        {
            Task.Run(() =>
            {
                _migrationProcessRepository.SaveAsync(new RedisData<JObject>()
                {
                    Data = JObject.FromObject(e.LogResult),
                    Key = e.LogResult.JobId
                }, $"Log${e.LogResult.JobId}$");
            });
        }

        private void LogDetailsPublisher_OnEntityChanged(object? sender, Repository.Delegates.LogDetailsEventArgs e)
        {
            Task.Run(async () =>
            {
                var redisValue = await _migrationProcessRepository.FindByKeyAsync(new RedisData<JObject>()
                {
                    Id = $"Log${e.LogDetail.JobId}$",
                    Key = e.LogDetail.JobId
                });

                var log = JsonConvert.DeserializeObject<LogResult>(redisValue.ToString());

                log.Details.Add(e.LogDetail);

                await _migrationProcessRepository.SaveAsync(new RedisData<JObject>()
                {
                    Data = JObject.FromObject(log),
                    Key = e.LogDetail.JobId
                }, $"Log${e.LogDetail.JobId}$");
            });
        }
    }
}