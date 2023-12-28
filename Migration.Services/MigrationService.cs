using Migration.Infrastructure.Redis;
using Migration.Infrastructure.Redis.Entities;
using Migration.Repository;
using Migration.Repository.Exceptions;
using Migration.Repository.Extensions;
using Migration.Repository.Helpers;
using Migration.Repository.Models;
using Migration.Services.Helpers;
using Migration.Services.LogModels;
using Migration.Services.Publishers;
using Newtonsoft.Json.Linq;

namespace Migration.Services
{
    public interface IMigrationService
    {
        Task Migrate(DataMapping dataMapping);
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
        }

        public async Task Migrate(DataMapping dataMapping)
        {
            List<Task> processTasks = new();

            LogResult log = new()
            {
                EntityName = dataMapping.DataQueryMappingType == DataQueryMappingType.UpdateAnotherCollection ? dataMapping.Destination.Settings.CurrentEntity
                    : dataMapping.Source.Settings.CurrentEntity,
                StartedIn = DateTime.Now,
                Description = $"Processing migration for Operation: {dataMapping.OperationType}."
            };

            _logResultPublisher.Publish(log);

            var jobId = await CreateJob(dataMapping);

            bool hasRecord;
            int skip = 0;
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
                        foreach (var sourceData in source)
                        {
                            processTasks.Add(ProcessDestinationRecordsAsync(dataMapping, sourceData, jobId));
                        }

                        //TODO: Subscribe an event to add the records already processed, can save pagination OFFSET <offset_amount> LIMIT <limit_amount>
                        //https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query/offset-limit

                        await Task.WhenAll(processTasks);
                    }
                    else
                    {
                        foreach (var sourceData in source)
                        {
                            processTasks.Add(ProcessSourceRecordsAsync(repository, dataMapping, sourceData, jobId));
                        }

                        await Task.WhenAll(processTasks);
                    }
                    skip += take;
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

        private async Task<string> CreateJob(DataMapping dataMapping)
        {
            var jObject = new JObject();
            jObject.Add("ProfileId", dataMapping.Id);
            jObject.Add("OperationType", dataMapping.OperationType.ToString());
            jObject.Add("Status", "In Progress");

            var jobs = await _migrationProcessRepository.CountAsync("Jobs");
            var jobId = jobs +1;

            jObject.Add("JobId", jobId);

            await _migrationProcessRepository.SaveAsync(new RedisData<JObject>()
            {
                Data = jObject,
                Key = jobId.ToString(),
                Id = jobId.ToString()
            }, "Jobs");

            return jobId.ToString();
        }

        private async Task ProcessSourceRecordsAsync(IGenericRepository repository, DataMapping dataMapping,
            KeyValuePair<string, string> data, string jobId)
        {
            var originalData = JObject.Parse(data.Value);

            LogDetails logDetails = new()
            {
                Display = true,
                Title = originalData["id"].ToString(),
            };

            try
            {
                if (dataMapping.OperationType == OperationType.Update)
                {
                    Task.Run(async () => await SaveCopyInLocal(dataMapping.Source.Settings.CurrentEntity, JObject.Parse(data.Value), "backup", jobId));
                    logDetails.Details.Add("Creating copy of the original data");

                    foreach (var mappingMergeField in dataMapping.FieldsMapping.Where(w =>
                                 w.MappingType == MappingType.UpdateValue))
                    {
                        var fieldArr = mappingMergeField.SourceField.Split(".").ToList();

                        var objectToBeUpdated = JObjectHelper.GetObject(originalData, fieldArr, mappingMergeField.ValueField);

                        Task.Run(async () => await SaveCopyInLocal(dataMapping.Source.Settings.CurrentEntity, objectToBeUpdated, "updated", jobId));
                        logDetails.Details.Add("Creating copy of the changes");

                        var differences = DifferenceHelper.FindDifferences(JObject.Parse(data.Value), objectToBeUpdated);

                        if (differences.Any())
                        {
                            logDetails.Details.Add("Values updated: " + string.Join(",", differences.Select(s => s.PropertyName + " = " + s.Object2Value)));
                            await repository.Update(objectToBeUpdated);
                        }
                    }
                }
                else if (dataMapping.OperationType == OperationType.Report)
                {
                    Task.Run(async () => await SaveCopyInLocal(dataMapping.Source.Settings.CurrentEntity, originalData, "report", jobId));
                    logDetails.Details.Add("Record exported");
                }
                else if (dataMapping.OperationType == OperationType.Delete)
                {
                    Task.Run(async () => await SaveCopyInLocal(dataMapping.Source.Settings.CurrentEntity, JObject.Parse(data.Value), "backup", jobId));
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
            KeyValuePair<string, string> sourceData, string jobId)
        {
            int skip = 0;
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
                    processTasks.Add(UpdateDestinationRecordsAsync(dataMapping, originalData, sourceObj, repository, jobId));
                }

                await Task.WhenAll(processTasks);

                skip += take;
            } while (hasRecord);
        }

        private async Task<bool> UpdateDestinationRecordsAsync(DataMapping dataMapping, JObject originalData, JObject sourceObj, IGenericRepository repository, string jobId)
        {
            LogDetails logDetails = new()
            {
                Display = true,
                Title = originalData["id"].ToString(),
            };

            try
            {
                if (dataMapping.OperationType == OperationType.Update)
                {
                    logDetails.Details.Add("Creating copy of the original data");
                    Task.Run(async () =>
                        await SaveCopyInLocal(dataMapping.Destination.Settings.CurrentEntity, originalData, "backup", jobId));

                    bool hasChange = false;

                    var objectToBeUpdated = UpdateDataHelper.UpdateObject(originalData.ToString(), dataMapping.FieldsMapping,
                        sourceObj, dataMapping.DataQueryMappingType, ref hasChange);

                    if (!hasChange) return true;

                    Task.Run(async () =>
                        await SaveCopyInLocal(dataMapping.Destination.Settings.CurrentEntity, objectToBeUpdated, "updated", jobId));
                    logDetails.Details.Add("Creating copy of the changes");

                    var differences = DifferenceHelper.FindDifferences(originalData, objectToBeUpdated);
                    logDetails.Details.Add("Values updated: " +
                                           string.Join(",", differences.Select(s => s.PropertyName + " = " + s.Object2Value)));

                    await repository.Update(objectToBeUpdated);
                }
                else if (dataMapping.OperationType == OperationType.Report)
                {
                    Task.Run(async () => await SaveCopyInLocal(dataMapping.Destination.Settings.CurrentEntity, originalData, "report", jobId));
                    logDetails.Details.Add("Record exported for analyse");
                }
                else if (dataMapping.OperationType == OperationType.Delete)
                {
                    logDetails.Details.Add("Creating copy of the original data");
                    Task.Run(async () =>
                        await SaveCopyInLocal(dataMapping.Destination.Settings.CurrentEntity, originalData, "backup", jobId));

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

        private async Task SaveCopyInLocal(string entity, JObject data, string suffix, string jobId)
        {
            var redisData = new RedisData<JObject>()
            {
                Key = $"{data["id"]}-{suffix}",
                Data = data
            };

            await _migrationProcessRepository.SaveAsync(redisData, $"{entity}${jobId}$");
        }
    }
}