using Migration.Infrastructure.Redis;
using Migration.Infrastructure.Redis.Entities;
using Migration.Repository;
using Migration.Repository.Exceptions;
using Migration.Repository.Extensions;
using Migration.Repository.LogModels;
using Migration.Repository.Models;
using Migration.Repository.Publishers;
using Migration.Services.Helpers;
using Migration.Services.Subscribers;
using Newtonsoft.Json.Linq;

namespace Migration.Services
{
    public interface IMigrationService
    {
        Task Migrate(Profile profile, int jobId);
        Task ImportData(List<JObject> data, DataSettings dataSettings);
    }

    public class MigrationService : IMigrationService
    {
        private readonly Func<DataSettings, IGenericRepository> _genericRepository;
        private readonly IRepository<JObject> _migrationProcessRepository;
        private readonly LogPublisher _logResultPublisher;
        private readonly LogDetailsPublisher _logDetailsPublisher;
        private readonly IJobService _jobService;

        public MigrationService(Func<DataSettings, IGenericRepository> genericRepository,
            IRepository<JObject> migrationProcessRepository,
            IJobService jobService,
            LogPublisher logPublisher,
            LogDetailsPublisher logDetailsPublisher,
            MigrationLogPersistSubscriber migrationLogSubscriber)
        {
            _genericRepository = genericRepository;
            _migrationProcessRepository = migrationProcessRepository;
            _jobService = jobService;
            _logResultPublisher = logPublisher;
            _logDetailsPublisher = logDetailsPublisher;

            _logResultPublisher.OnEntityChanged += migrationLogSubscriber.LogResultPublisher_OnEntityChanged;
            _logDetailsPublisher.OnEntityChanged += migrationLogSubscriber.LogDetailsPublisher_OnEntityChanged;
        }

        public async Task ImportData(List<JObject> listData, DataSettings dataSettings)
        {
            var repository = _genericRepository(dataSettings);

            foreach (var data in listData)
            {
                await repository.Insert(data);
            }
        }

        public async Task Migrate(Profile profile, int jobId)
        {
            List<Task> processTasks = new();
            var job = await _jobService.GetOrCreateJob(profile, jobId);

            var dataMapping = profile.DataMappings[0];

            LogResult log = new()
            {
                EntityName = dataMapping.DataQueryMappingType == DataQueryMappingType.UpdateAnotherCollection ? dataMapping.Destination.Settings.CurrentEntity
                    : dataMapping.Source.Settings.CurrentEntity,
                StartedIn = DateTime.Now,
                Description = $"Processing migration for Operation: {dataMapping.OperationType}.",
                JobId = job.JobId
            };

            _logResultPublisher.Publish(log);

            bool hasRecord;
            int skip = job.SourceProcessed;

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

                        skip = job.SourceProcessed;
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

                        skip = job.SourceProcessed;

                        skip += take;
                        job.SourceProcessed = skip;
                        await _jobService.UpdateJob(job);
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

        private async Task ProcessSourceRecordsAsync(IGenericRepository repository, DataMapping dataMapping,
            KeyValuePair<string, string> data, Jobs job)
        {
            var originalData = JObject.Parse(data.Value);
            LogDetails logDetails = new()
            {
                Display = true,
                Title = data.Key,
                JobId = job.JobId
            };

            try
            {
                JObject backup = new JObject();
                if (dataMapping.OperationType == OperationType.Update)
                {
                    logDetails.Descriptions.Add(new("Creating copy of the original data"));

                    bool hasChange = false;

                    var objectToBeUpdated = UpdateDataHelper.UpdateObject(data.Value, dataMapping.FieldsMapping, ref hasChange);

                    backup.Add("id", originalData["id"].ToString());
                    backup.Add("Backup", originalData);
                    backup.Add("Updated", objectToBeUpdated);
                    await SaveCopyInLocal(dataMapping.Source.Settings.CurrentEntity, backup, job);

                    if (!hasChange) return;

                    var differences = DifferenceHelper.FindDifferences(JObject.Parse(data.Value), objectToBeUpdated);

                    if (differences.Any())
                    {
                        logDetails.Descriptions.Add(new("Values updated: " + string.Join(",", differences.Select(s => s.PropertyName + " = " + s.Object2Value))));
                        await repository.Update(objectToBeUpdated, dataMapping.FieldsMapping);
                    }
                }
                else if (dataMapping.OperationType == OperationType.Report)
                {
                    backup.Add("id", originalData["id"].ToString());
                    backup.Add("Report", originalData);
                    await SaveCopyInLocal(dataMapping.Source.Settings.CurrentEntity, backup, job);

                    logDetails.Descriptions.Add(new("Record exported"));
                }
                else if (dataMapping.OperationType == OperationType.Delete)
                {
                    backup.Add("id", originalData["id"].ToString());
                    backup.Add("Deleted", originalData);
                    await SaveCopyInLocal(dataMapping.Source.Settings.CurrentEntity, backup, job);

                    logDetails.Descriptions.Add(new("Creating copy of the original data"));
                    await repository.Delete(originalData);
                    logDetails.Descriptions.Add(new("Record deleted"));
                }
            }
            catch (DbOperationException e)
            {
                logDetails.Type = LogType.Error;
                logDetails.Descriptions.Add(new($"Error: {e.ErrorCode} - {e.ErrorMessage}"));
            }
            catch (Exception e)
            {
                logDetails.Type = LogType.Error;
                logDetails.Descriptions.Add(new($"Error: {e.Message}"));
            }

            _logDetailsPublisher.Publish(logDetails);
        }

        private async Task ProcessDestinationRecordsAsync(DataMapping dataMapping,
            KeyValuePair<string, string> sourceData, Jobs job)
        {
            int skip = job.DestinationProcessed;
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
                job.DestinationProcessed = skip;
                await _jobService.UpdateJob(job);
            } while (hasRecord);

            job.DestinationProcessed = 0;
            int sourceSkip = job.SourceProcessed;

            sourceSkip++;
            job.SourceProcessed = sourceSkip;

            await _jobService.UpdateJob(job);
        }

        private async Task<bool> UpdateDestinationRecordsAsync(DataMapping dataMapping, JObject originalData, JObject sourceObj, IGenericRepository repository, Jobs job)
        {
            LogDetails logDetails = new()
            {
                Display = true,
                Title = originalData["id"].ToString(),
                JobId = job.JobId
            };

            JObject backup = new JObject();

            try
            {
                if (dataMapping.OperationType == OperationType.Update)
                {
                    logDetails.Descriptions.Add(new("Creating copy of the original data"));

                    bool hasChange = false;

                    var objectToBeUpdated = UpdateDataHelper.UpdateObject(originalData.ToString(), dataMapping.FieldsMapping, sourceObj, ref hasChange);

                    if (!hasChange) return true;

                    backup.Add("id", originalData["id"].ToString());
                    backup.Add("Backup", originalData);
                    backup.Add("Updated", objectToBeUpdated);

                    await SaveCopyInLocal(dataMapping.Destination.Settings.CurrentEntity, backup, job);

                    logDetails.Descriptions.Add(new("Creating copy of the changes"));

                    var differences = DifferenceHelper.FindDifferences(originalData, objectToBeUpdated);
                    logDetails.Descriptions.Add(new("Values updated: " +
                                                string.Join(",", differences.Select(s => s.PropertyName + " = " + s.Object2Value))));

                    await repository.Update(objectToBeUpdated, dataMapping.FieldsMapping);
                }
                else if (dataMapping.OperationType == OperationType.Report)
                {
                    backup.Add("id", originalData["id"].ToString());
                    backup.Add("Report", originalData);

                    await SaveCopyInLocal(dataMapping.Destination.Settings.CurrentEntity, backup, job);
                    logDetails.Descriptions.Add(new("Record exported for analyse"));
                }
                else if (dataMapping.OperationType == OperationType.Delete)
                {
                    logDetails.Descriptions.Add(new("Creating copy of the original data"));

                    backup.Add("id", originalData["id"].ToString());
                    backup.Add("Deleted", originalData);
                    await SaveCopyInLocal(dataMapping.Destination.Settings.CurrentEntity, backup, job);

                    await repository.Delete(originalData);
                    logDetails.Descriptions.Add(new("Record deleted"));
                }
            }
            catch (DbOperationException e)
            {
                logDetails.Descriptions.Add(new($"Error: {e.ErrorCode} - {e.ErrorMessage}"));
                logDetails.Type = LogType.Error;
            }

            _logDetailsPublisher.Publish(logDetails);
            return false;
        }

        private async Task SaveCopyInLocal(string entity, JObject data, Jobs job)
        {
            var redisData = new RedisData<JObject>()
            {
                RedisValue = $"{data["id"]}",
                Data = data,
                RedisKey = $"{entity}${job.JobCategory}${job.JobId}$"
            };

            await _migrationProcessRepository.SaveAsync(redisData);
        }
    }
}