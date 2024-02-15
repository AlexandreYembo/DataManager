using Migration.Core;
using Migration.Core.Exceptions;
using Migration.EventHandlers.Publishers;
using Migration.Infrastructure.Redis;
using Migration.Infrastructure.Redis.Entities;
using Migration.Models;
using Migration.Models.Logs;
using Migration.Models.Profile;
using Migration.Services.Extensions;
using Migration.Services.Helpers;
using Newtonsoft.Json.Linq;

namespace Migration.Services
{
    public interface IMigrationService
    {
        Task Migrate(ProfileConfiguration profile, Jobs job);
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
                RepositoryParameters repositoryParameters = new()
                {
                    Data = data,
                    //FieldMappings TODO: pass the value here
                };

                await repository.InsertAsync(repositoryParameters);
            }
        }

        public async Task Migrate(ProfileConfiguration profile, Jobs job)
        {

            List<Task> processTasks = new();

            LogResult log = new()
            {
                EntityName = profile.DataQueryMappingType == DataQueryMappingType.SourceToTarget ? profile.Target.Settings.CurrentEntity.Name
                    : profile.Source.Settings.CurrentEntity.Name,
                StartedIn = DateTime.Now,
                Description = $"Processing migration for Operation: {profile.OperationType}.",
                JobId = job.JobId,
                OperationType = profile.OperationType
            };
            await _logResultPublisher.PublishAsync(log);

            try
            {
                job.Start();
                await _jobService.UpdateJob(job);

                bool hasRecord;
                int skip = job.SourceProcessed;

                int take = 15;

                var sourceRepository = _genericRepository(profile.Source.Settings);
                var targetRepository = _genericRepository(profile.Target.Settings);

                if (profile.DataQueryMappingType == DataQueryMappingType.SourceToTarget && profile.OperationType == OperationType.Import)
                {
                    //Create new table for the Target
                    await targetRepository.CreateTableAsync();
                }

                do
                {
                    RepositoryParameters repositoryParameters = new()
                    {
                        Query = profile.Source.Query,
                        FieldMappings = profile.FieldsMapping,
                        Pagination = new()
                        {
                            Skip = skip,
                            Take = take
                        }
                    };

                    var source = await sourceRepository.GetAsync(repositoryParameters);
                    log.TotalRecords += source.Count;

                    if (source.Any())
                    {
                        if (profile.DataQueryMappingType == DataQueryMappingType.SourceToTarget)
                        {
                            foreach (var sourceData in source.Where(w => w.Value != null))
                            {
                                await ProcessTargetRecordsAsync(targetRepository, profile, sourceData, job);
                            }

                            skip = job.SourceProcessed;
                            //TODO: Subscribe an event to add the records already processed, can save pagination OFFSET <offset_amount> LIMIT <limit_amount>
                            //https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query/offset-limit
                        }
                        else
                        {
                            foreach (var sourceData in source)
                            {
                                processTasks.Add(ProcessSourceRecordsAsync(sourceRepository, profile, sourceData, job));
                            }

                            await Task.WhenAll(processTasks);

                            //we do not apply pagination because we are removing records from the table, should aways take the first page
                            if (profile.OperationType != OperationType.Delete)
                            {
                                skip = job.SourceProcessed;
                                skip += take;

                                job.SourceProcessed = skip;

                                await _jobService.UpdateJob(job);
                            }

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
            catch (TableOperationException e)
            {
                await _actionsPublisher.PublishAsync(new Actions()
                {
                    ActionType = ActionEventType.Error,
                    Message = "Error during the migration, check the logs for more details!"
                });

                LogDetails detailsError = new()
                {
                    LogDateTime = DateTime.Now,
                    Title = "Error Migration - Data Base exception",
                    Descriptions = new()
                    {
                        new($"Error: {e.ErrorCode} - {e.ErrorMessage}")
                    },
                    Display = true,
                    JobId = job.JobId,
                    Type = LogType.Error,
                    OperationType = profile.OperationType
                };
                await _logDetailsPublisher.PublishAsync(detailsError);
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
                    OperationType = profile.OperationType
                };

                await _logDetailsPublisher.PublishAsync(detailsError);
            }

            log.FinishedIn = DateTime.Now;
            await _logResultPublisher.PublishAsync(log);
        }

        private async Task ProcessSourceRecordsAsync(IGenericRepository repository, ProfileConfiguration profile,
            KeyValuePair<string, JObject> data, Jobs job)
        {
            var originalData = data.Value;
            LogDetails logDetails = new()
            {
                Display = true,
                Title = data.Key,
                JobId = job.JobId,
                OperationType = profile.OperationType
            };

            JObject backup = new JObject();
            if (profile.OperationType == OperationType.Update)
            {
                logDetails.Descriptions.Add(new("Creating copy of the original data"));

                bool hasChange = false;

                var objectToBeUpdated = UpdateDataHelper.UpdateObject(data.Value.ToString(), profile.FieldsMapping, ref hasChange);

                backup.Add("id", originalData["id"].ToString());
                backup.Add("Backup", originalData);
                backup.Add("Updated", objectToBeUpdated);
                await SaveCopyInLocal(profile.Source.Settings.CurrentEntity.Name, backup, job);

                if (!hasChange) return;

                var differences = DifferenceHelper.FindDifferences(data.Value, objectToBeUpdated);

                if (differences.Any())
                {
                    logDetails.Descriptions.Add(new("Values updated: " + string.Join(",",
                        differences.Select(s => s.PropertyName + " = " + s.Object2Value))));

                    RepositoryParameters repositoryParameters = new()
                    {
                        Data = objectToBeUpdated,
                        FieldMappings = profile.FieldsMapping
                    };

                    await repository.UpdateAsync(repositoryParameters);
                }
            }
            else if (profile.OperationType == OperationType.Report)
            {
                bool hasChange = false;

                var objectToBeUpdated = UpdateDataHelper.UpdateObject(data.Value.ToString(), profile.FieldsMapping, ref hasChange);

                if (!hasChange) return;

                backup.Add("id", originalData["id"].ToString());
                backup.Add("Report", objectToBeUpdated);
                await SaveCopyInLocal(profile.Source.Settings.CurrentEntity.Name, backup, job);

                logDetails.Descriptions.Add(new("Record exported"));
            }
            else if (profile.OperationType == OperationType.Delete)
            {
                backup.Add("id", originalData["id"].ToString());
                backup.Add("Deleted", originalData);
                await SaveCopyInLocal(profile.Source.Settings.CurrentEntity.Name, backup, job);

                logDetails.Descriptions.Add(new("Creating copy of the original data"));

                RepositoryParameters repositoryParameters = new()
                {
                    Data = originalData,
                    FieldMappings = profile.FieldsMapping
                };

                await repository.DeleteAsync(repositoryParameters);
                logDetails.Descriptions.Add(new("Record deleted"));
            }
            else if (profile.OperationType == OperationType.Import)
            {
                throw new Exception("Operation not supported");
            }

            await _logDetailsPublisher.PublishAsync(logDetails);
        }

        private async Task ProcessTargetRecordsAsync(IGenericRepository repository, ProfileConfiguration profile, KeyValuePair<string, JObject> sourceData, Jobs job)
        {
            int skip = job.TargetProcessed;
            int take = 15;
            bool hasRecord;

            List<string> targetIds = new();

            if (profile.OperationType == OperationType.Import) // TODO: implement an interface that will have their own concrete class implementing the logic for each type of OperationType
            {
                await UpdateTargetRecordsAsync(profile, new JObject(), sourceData.Value, repository, job);
            }
            else
            {
                do
                {
                    RepositoryParameters repositoryParameters = new()
                    {
                        Data = sourceData.Value,
                        FieldMappings = profile.FieldsMapping,
                        Query = profile.Target.Query,
                        Pagination = new()
                        {
                            Skip = skip,
                            Take = take
                        }
                    };

                    var target = await repository.GetAsync(repositoryParameters);

                    var listTarget = target.ApplyJoin(sourceData, profile.FieldsMapping);

                    hasRecord = listTarget.Any();

                    List<Task> processTasks = new();

                    foreach (var originalData in listTarget)
                    {
                        targetIds.Add(originalData["id"].ToString());
                        processTasks.Add(UpdateTargetRecordsAsync(profile, originalData, sourceData.Value, repository, job));
                    }

                    await Task.WhenAll(processTasks);

                    if (profile.OperationType != OperationType.Delete)
                    {
                        skip += take;
                        job.TargetProcessed = skip;
                        await _jobService.UpdateJob(job);
                    }
                } while (hasRecord);
            }

            job.TargetProcessed = 0;
            int sourceSkip = job.SourceProcessed;

            sourceSkip++;
            job.SourceProcessed = sourceSkip;

            await _jobService.UpdateJob(job);
        }

        private async Task UpdateTargetRecordsAsync(ProfileConfiguration profile, JObject targetData, JObject sourceObj, IGenericRepository repository, Jobs job)
        {
            string id = string.Empty;

            if (!targetData.Properties().Any())
            {
                id = Guid.NewGuid().ToString();
            }
            else
            {
                id = targetData.SelectToken("id") != null
                    ? targetData["id"].ToString()
                    : targetData.SelectToken(profile.Target.Settings.CurrentEntity.Attributes.FirstOrDefault().Value.Replace("/", string.Empty)).ToString();
            }

            LogDetails logDetails = new()
            {
                Display = true,
                Title = id,
                JobId = job.JobId,
                OperationType = profile.OperationType
            };

            JObject backup = new JObject();


            if (profile.OperationType == OperationType.Update)
            {
                logDetails.Descriptions.Add(new("Creating copy of the original data"));

                bool hasChange = false;

                var objectToBeUpdated = UpdateDataHelper.UpdateObject(targetData.ToString(), profile.FieldsMapping, sourceObj, ref hasChange);

                if (!hasChange) return;

                backup.Add("id", targetData["id"].ToString());
                backup.Add("Backup", targetData);
                backup.Add("Updated", objectToBeUpdated);

                await SaveCopyInLocal(profile.Target.Settings.CurrentEntity.Name, backup, job);

                logDetails.Descriptions.Add(new("Creating copy of the changes"));

                var differences = DifferenceHelper.FindDifferences(targetData, objectToBeUpdated);
                logDetails.Descriptions.Add(new("Values updated: " + string.Join(",", differences.Select(s => s.PropertyName + " = " + s.Object2Value))));

                RepositoryParameters repositoryParameters = new()
                {
                    Data = objectToBeUpdated,
                    FieldMappings = profile.FieldsMapping
                };

                await repository.UpdateAsync(repositoryParameters);
            }
            else if (profile.OperationType == OperationType.Report)
            {
                backup.Add("id", targetData["id"].ToString());
                backup.Add("Report", targetData);

                await SaveCopyInLocal(profile.Target.Settings.CurrentEntity.Name, backup, job);
                logDetails.Descriptions.Add(new("Record exported for analyse"));
            }
            else if (profile.OperationType == OperationType.Delete)
            {
                logDetails.Descriptions.Add(new("Creating copy of the original data"));

                backup.Add("id", targetData["id"].ToString());
                backup.Add("Deleted", targetData);
                await SaveCopyInLocal(profile.Target.Settings.CurrentEntity.Name, backup, job);

                RepositoryParameters repositoryParameters = new()
                {
                    Data = targetData,
                    FieldMappings = profile.FieldsMapping
                };

                await repository.DeleteAsync(repositoryParameters);

                logDetails.Descriptions.Add(new("Record deleted"));
            }
            else if (profile.OperationType == OperationType.Import)
            {
                bool hasChange = false;

                var propertyId = profile.Target.Settings.CurrentEntity.Attributes.FirstOrDefault(f => f.Key == TableAttributesType.RecordId.ToString()).Value;
                targetData[propertyId] = id;

                var objectToBeImported = UpdateDataHelper.UpdateObject(targetData.ToString(), profile.FieldsMapping, sourceObj, ref hasChange);

                if (!hasChange) return;

                backup.Add("id", id);
                backup.Add("Inserted", objectToBeImported);
                await SaveCopyInLocal(profile.Target.Settings.CurrentEntity.Name, backup, job);

                RepositoryParameters repositoryParameters = new()
                {
                    Data = objectToBeImported,
                    FieldMappings = profile.FieldsMapping
                };

                await repository.InsertAsync(repositoryParameters);

                logDetails.Descriptions.Add("Record imported");
            }

            await _logDetailsPublisher.PublishAsync(logDetails);
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