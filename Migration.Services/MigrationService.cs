using Migration.Infrastructure.Redis;
using Migration.Infrastructure.Redis.Entities;
using Migration.Repository;
using Migration.Repository.Delegates;
using Migration.Repository.Exceptions;
using Migration.Repository.Extensions;
using Migration.Repository.LogModels;
using Migration.Repository.Models;
using Migration.Repository.Publishers;
using Migration.Services.Helpers;
using Newtonsoft.Json.Linq;

namespace Migration.Services
{
    public interface IMigrationService
    {
        Task Migrate(Profile profile, Jobs job);
        Task ImportData(List<JObject> data, DataSettings dataSettings);
    }

    public class MigrationService : IMigrationService
    {
        private readonly Func<DataSettings, IGenericRepository> _genericRepository;
        private readonly IRepository<JObject> _migrationProcessRepository;
        private readonly LogPublisher _logResultPublisher;
        private readonly LogDetailsPublisher _logDetailsPublisher;
        private readonly IJobService _jobService;
        private readonly ActionsPublisher _actionsPublisher;

        public MigrationService(Func<DataSettings, IGenericRepository> genericRepository,
            IRepository<JObject> migrationProcessRepository,
            IJobService jobService,
            LogPublisher logPublisher,
            LogDetailsPublisher logDetailsPublisher,
            ActionsPublisher actionsPublisher)
        {
            _genericRepository = genericRepository;
            _migrationProcessRepository = migrationProcessRepository;
            _jobService = jobService;
            _logResultPublisher = logPublisher;
            _logDetailsPublisher = logDetailsPublisher;
            _actionsPublisher = actionsPublisher;
        }

        public async Task ImportData(List<JObject> listData, DataSettings dataSettings)
        {
            var repository = _genericRepository(dataSettings);

            foreach (var data in listData)
            {
                await repository.Insert(data);
            }
        }

        public async Task Migrate(Profile profile, Jobs job)
        {

            List<Task> processTasks = new();

            var dataMapping = profile.DataMappings[0];

            LogResult log = new()
            {
                EntityName = dataMapping.DataQueryMappingType == DataQueryMappingType.UpdateAnotherCollection ? dataMapping.Destination.Settings.CurrentEntity.Name
                    : dataMapping.Source.Settings.CurrentEntity.Name,
                StartedIn = DateTime.Now,
                Description = $"Processing migration for Operation: {dataMapping.OperationType}.",
                JobId = job.JobId,
                OperationType = profile.DataMappings[0].OperationType
            };
            await _logResultPublisher.PublishAsync(log);

            try
            {
                job.Start();
                await _jobService.UpdateJob(job);

                bool hasRecord;
                int skip = job.SourceProcessed;

                int take = 15;

                var sourceRepository = _genericRepository(dataMapping.Source.Settings);
                var destinationRepository = _genericRepository(dataMapping.Destination.Settings);

                if (dataMapping.DataQueryMappingType == DataQueryMappingType.UpdateAnotherCollection && dataMapping.OperationType == OperationType.Import)
                {
                    //Create new table for the destination
                    await destinationRepository.CreateTable();
                }

                do
                {
                    var source = await sourceRepository.Get(dataMapping.Source.Query, null, null, take, skip);
                    log.TotalRecords += source.Count;

                    if (source.Any())
                    {
                        if (dataMapping.DataQueryMappingType == DataQueryMappingType.UpdateAnotherCollection)
                        {
                            foreach (var sourceData in source.Where(w => w.Value != "{}" || w.Value != "null"))
                            {
                                await ProcessDestinationRecordsAsync(destinationRepository, dataMapping, sourceData, job);
                            }

                            skip = job.SourceProcessed;
                            //TODO: Subscribe an event to add the records already processed, can save pagination OFFSET <offset_amount> LIMIT <limit_amount>
                            //https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query/offset-limit
                        }
                        else
                        {
                            foreach (var sourceData in source)
                            {
                                processTasks.Add(ProcessSourceRecordsAsync(sourceRepository, dataMapping, sourceData, job));
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

                job.Complete();

                await _jobService.UpdateJob(job);

                await _actionsPublisher.PublishAsync(new Actions()
                {
                    ActionType = ActionEventType.Success,
                    Message = "Migration completed!"
                });
            }
            catch (Exception ex)
            {
                await _actionsPublisher.PublishAsync(new Actions()
                {
                    ActionType = ActionEventType.Error,
                    Message = "Error during the migration, check the logs for more details!"
                });

                LogDetails detailsError = new()
                {
                    LogDateTime = DateTime.Now,
                    Title = "Error Migration",
                    Descriptions = new()
                    {
                        ex.Message
                    },
                    Display = true,
                    JobId = job.JobId,
                    Type = LogType.Error,
                    OperationType = profile.DataMappings[0].OperationType
                };

                await _logDetailsPublisher.PublishAsync(detailsError);
            }

            log.FinishedIn = DateTime.Now;
            await _logResultPublisher.PublishAsync(log);
        }

        private async Task ProcessSourceRecordsAsync(IGenericRepository repository, DataMapping dataMapping,
            KeyValuePair<string, string> data, Jobs job)
        {
            var originalData = JObject.Parse(data.Value);
            LogDetails logDetails = new()
            {
                Display = true,
                Title = data.Key,
                JobId = job.JobId,
                OperationType = dataMapping.OperationType
            };

            try
            {
                JObject backup = new JObject();
                if (dataMapping.OperationType == OperationType.Update)
                {
                    logDetails.Descriptions.Add(new("Creating copy of the original data"));

                    bool hasChange = false;

                    var objectToBeUpdated =
                        UpdateDataHelper.UpdateObject(data.Value, dataMapping.FieldsMapping, ref hasChange);

                    backup.Add("id", originalData["id"].ToString());
                    backup.Add("Backup", originalData);
                    backup.Add("Updated", objectToBeUpdated);
                    await SaveCopyInLocal(dataMapping.Source.Settings.CurrentEntity.Name, backup, job);

                    if (!hasChange) return;

                    var differences = DifferenceHelper.FindDifferences(JObject.Parse(data.Value), objectToBeUpdated);

                    if (differences.Any())
                    {
                        logDetails.Descriptions.Add(new("Values updated: " + string.Join(",",
                            differences.Select(s => s.PropertyName + " = " + s.Object2Value))));
                        await repository.Update(objectToBeUpdated, dataMapping.FieldsMapping);
                    }
                }
                else if (dataMapping.OperationType == OperationType.Report)
                {
                    bool hasChange = false;

                    var objectToBeUpdated =
                       UpdateDataHelper.UpdateObject(data.Value, dataMapping.FieldsMapping, ref hasChange);

                    if (!hasChange) return;

                    backup.Add("id", originalData["id"].ToString());
                    backup.Add("Report", objectToBeUpdated);
                    await SaveCopyInLocal(dataMapping.Source.Settings.CurrentEntity.Name, backup, job);

                    logDetails.Descriptions.Add(new("Record exported"));
                }
                else if (dataMapping.OperationType == OperationType.Delete)
                {
                    backup.Add("id", originalData["id"].ToString());
                    backup.Add("Deleted", originalData);
                    await SaveCopyInLocal(dataMapping.Source.Settings.CurrentEntity.Name, backup, job);

                    logDetails.Descriptions.Add(new("Creating copy of the original data"));
                    await repository.Delete(originalData);
                    logDetails.Descriptions.Add(new("Record deleted"));
                }
                else if (dataMapping.OperationType == OperationType.Import)
                {
                    throw new Exception("Operation not supported");
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

            await _logDetailsPublisher.PublishAsync(logDetails);
        }

        private async Task ProcessDestinationRecordsAsync(IGenericRepository repository, DataMapping dataMapping,
            KeyValuePair<string, string> sourceData, Jobs job)
        {
            int skip = job.DestinationProcessed;
            int take = 15;
            bool hasRecord;

            List<string> destinationIds = new();

            if (dataMapping.OperationType == OperationType.Import) // TODO: implement an interface that will have their own concrete class implementing the logic for each type of OperationType
            {
                var sourceObj = JObject.Parse(sourceData.Value);

                await UpdateDestinationRecordsAsync(dataMapping, new JObject(), sourceObj, repository, job);
            }
            else
            {
                do
                {
                    var destination = await repository.Get(dataMapping.Destination.Query, dataMapping.FieldsMapping, sourceData.Value, take, skip);

                    var listDestination = destination.ApplyJoin(sourceData, dataMapping.FieldsMapping);

                    hasRecord = listDestination.Any();

                    var sourceObj = JObject.Parse(sourceData.Value);
                    List<Task> processTasks = new();

                    foreach (var originalData in listDestination)
                    {
                        destinationIds.Add(originalData["id"].ToString());
                        processTasks.Add(UpdateDestinationRecordsAsync(dataMapping, originalData, sourceObj, repository, job));
                    }

                    await Task.WhenAll(processTasks);

                    skip += take;
                    job.DestinationProcessed = skip;
                    await _jobService.UpdateJob(job);

                } while (hasRecord);
            }

            job.DestinationProcessed = 0;
            int sourceSkip = job.SourceProcessed;

            sourceSkip++;
            job.SourceProcessed = sourceSkip;

            await _jobService.UpdateJob(job);
        }

        private async Task<bool> UpdateDestinationRecordsAsync(DataMapping dataMapping, JObject destinationData, JObject sourceObj, IGenericRepository repository, Jobs job)
        {
            string id = string.Empty;

            if (!destinationData.Properties().Any())
            {
                id = sourceObj.SelectToken("id") != null
                    ? sourceObj["id"].ToString()
                    : sourceObj.SelectToken(dataMapping.Destination.Settings.CurrentEntity.Attributes.FirstOrDefault().Value.Replace("/", string.Empty)).ToString();
            }
            else
            {
                id = destinationData.SelectToken("id") != null
                    ? destinationData["id"].ToString()
                    : destinationData.SelectToken(dataMapping.Destination.Settings.CurrentEntity.Attributes.FirstOrDefault().Value.Replace("/", string.Empty)).ToString();
            }

            LogDetails logDetails = new()
            {
                Display = true,
                Title = id,
                JobId = job.JobId,
                OperationType = dataMapping.OperationType
            };

            JObject backup = new JObject();

            try
            {
                if (dataMapping.OperationType == OperationType.Update)
                {
                    logDetails.Descriptions.Add(new("Creating copy of the original data"));

                    bool hasChange = false;

                    var objectToBeUpdated = UpdateDataHelper.UpdateObject(destinationData.ToString(), dataMapping.FieldsMapping, sourceObj, ref hasChange);

                    if (!hasChange) return true;

                    backup.Add("id", destinationData["id"].ToString());
                    backup.Add("Backup", destinationData);
                    backup.Add("Updated", objectToBeUpdated);

                    await SaveCopyInLocal(dataMapping.Destination.Settings.CurrentEntity.Name, backup, job);

                    logDetails.Descriptions.Add(new("Creating copy of the changes"));

                    var differences = DifferenceHelper.FindDifferences(destinationData, objectToBeUpdated);
                    logDetails.Descriptions.Add(new("Values updated: " +
                                                string.Join(",", differences.Select(s => s.PropertyName + " = " + s.Object2Value))));

                    await repository.Update(objectToBeUpdated, dataMapping.FieldsMapping);
                }
                else if (dataMapping.OperationType == OperationType.Report)
                {
                    backup.Add("id", destinationData["id"].ToString());
                    backup.Add("Report", destinationData);

                    await SaveCopyInLocal(dataMapping.Destination.Settings.CurrentEntity.Name, backup, job);
                    logDetails.Descriptions.Add(new("Record exported for analyse"));
                }
                else if (dataMapping.OperationType == OperationType.Delete)
                {
                    logDetails.Descriptions.Add(new("Creating copy of the original data"));

                    backup.Add("id", destinationData["id"].ToString());
                    backup.Add("Deleted", destinationData);
                    await SaveCopyInLocal(dataMapping.Destination.Settings.CurrentEntity.Name, backup, job);

                    await repository.Delete(destinationData);
                    logDetails.Descriptions.Add(new("Record deleted"));
                }
                else if (dataMapping.OperationType == OperationType.Import)
                {
                    bool hasChange = false;

                    var propertyId = dataMapping.Destination.Settings.CurrentEntity.Attributes.FirstOrDefault(f => f.Key == TableAttributesType.RecordId.ToString()).Value;
                    destinationData[propertyId] = id;

                    var objectToBeImported = UpdateDataHelper.UpdateObject(destinationData.ToString(), dataMapping.FieldsMapping, sourceObj, ref hasChange);

                    if (!hasChange) return true;

                    backup.Add("id", id);
                    backup.Add("Inserted", objectToBeImported);
                    await SaveCopyInLocal(dataMapping.Destination.Settings.CurrentEntity.Name, backup, job);

                    await repository.Insert(objectToBeImported, dataMapping.FieldsMapping);

                    logDetails.Descriptions.Add("Record imported");
                }
            }
            catch (DbOperationException e)
            {
                logDetails.Descriptions.Add(new($"Error: {e.ErrorCode} - {e.ErrorMessage}"));
                logDetails.Type = LogType.Error;
            }

            await _logDetailsPublisher.PublishAsync(logDetails);
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