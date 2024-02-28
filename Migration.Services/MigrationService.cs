using Connectors.Redis;
using Migration.Core;
using Migration.Core.Exceptions;
using Migration.EventHandlers.Publishers;
using Migration.Models;
using Migration.Models.Logs;
using Migration.Models.Profile;
using Migration.Services.Extensions;
using Migration.Services.Operations;
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
        private readonly Func<ProfileConfiguration, IOperation> _operation;
        private readonly LogPublisher _logResultPublisher;
        private readonly LogDetailsPublisher _logDetailsPublisher;
        private readonly IJobService _jobService;
        private readonly ActionsPublisher _actionsPublisher;
        private readonly ICacheRepository _cacheRepository;

        public MigrationService(Func<DataSettings, IGenericRepository> genericRepository,
            Func<ProfileConfiguration, IOperation> operation,
            IJobService jobService,
            LogPublisher logPublisher,
            LogDetailsPublisher logDetailsPublisher,
            ICacheRepository cacheRepository,
            ActionsPublisher actionsPublisher)
        {
            _genericRepository = genericRepository;
            _operation = operation;
            _jobService = jobService;
            _logResultPublisher = logPublisher;
            _logDetailsPublisher = logDetailsPublisher;
            _actionsPublisher = actionsPublisher;
            _cacheRepository = cacheRepository;
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
                var operationResolved = _operation(profile);

                job.Start();
                await _jobService.UpdateJob(job);

                bool hasRecord;
                int skip = job.SourceProcessed;

                int take = 15;

                IGenericRepository sourceRepository = null;
                var targetRepository = _genericRepository(profile.Target.Settings);

                Dictionary<string, JObject> sourceFromCache = new();

                if (profile.Source.Settings.IsCacheConnection)
                {
                    sourceFromCache = await _cacheRepository.GetAsync(profile.Source.Settings.CurrentEntity.Name);
                }
                else
                {
                    sourceRepository = _genericRepository(profile.Source.Settings);
                }

                if (profile.DataQueryMappingType == DataQueryMappingType.SourceToTarget && profile.OperationType == OperationType.Import)
                {
                    //Create new table for the Target
                    await targetRepository.CreateTableAsync();
                }

                do
                {
                    List<Task> processTasks = new();

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

                    Dictionary<string, JObject> source;

                    if (profile.Source.Settings.IsCacheConnection)
                    {
                        source = sourceFromCache.Skip(skip).Take(15).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    }
                    else
                    {
                        source = await sourceRepository.GetAsync(repositoryParameters);
                    }

                    log.TotalRecords += source.Count;

                    if (source.Any())
                    {
                        if (profile.DataQueryMappingType == DataQueryMappingType.SourceToTarget)
                        {
                            foreach (var sourceData in source.Where(w => w.Value != null))
                            {
                                await ProcessTargetRecordsAsync(targetRepository, operationResolved, profile, sourceData, job);
                            }

                            skip = job.SourceProcessed;
                            //TODO: Subscribe an event to add the records already processed, can save pagination OFFSET <offset_amount> LIMIT <limit_amount>
                            //https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query/offset-limit
                        }
                        else
                        {
                            foreach (var sourceData in source)
                            {
                                processTasks.Add(operationResolved.ProcessDataAsync(sourceRepository, new List<(EntityType entityType, string id, JObject data)>
                                {
                                    new (EntityType.Source, sourceData.Key, sourceData.Value)
                                }, profile, job));
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

                    hasRecord = source.Count == take; // if the list of Record is less than the take, it means that there will no have any more data to be checked.
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

                job.Status = JobStatus.Error;
                await _jobService.UpdateJob(job);
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

                job.Status = JobStatus.Error;
                await _jobService.UpdateJob(job);
            }

            log.FinishedIn = DateTime.Now;
            await _logResultPublisher.PublishAsync(log);
        }


        private async Task ProcessTargetRecordsAsync(IGenericRepository repository, IOperation operation, ProfileConfiguration profile, 
            KeyValuePair<string, JObject> sourceData, Jobs job)
        {
            int skip = job.TargetProcessed;
            int take = 15;
            bool hasRecord;

            if (profile.OperationType == OperationType.Import)
            {
                await operation.ProcessDataAsync(repository, new List<(EntityType entityType, string id, JObject data)>
                                {
                                    new (EntityType.Source, sourceData.Key, sourceData.Value),
                                }, profile, job);
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

                    Dictionary<string, JObject> target;

                    if (profile.Target.Settings.IsCacheConnection)
                    {
                        target = await _cacheRepository.GetHashAsync(new RepositoryParameters()
                        {
                            Query = profile.Target.Query,
                            FieldMappings = profile.FieldsMapping,
                            Entity = profile.Target.Settings.CurrentEntity.Name,
                            Data = sourceData.Value,
                            Pagination = new()
                            {
                                Take = take,
                                Skip = 0
                            }
                        });
                    }
                    else
                    {
                        target = await repository.GetAsync(repositoryParameters);
                    }

                    var listTarget = target.ApplyJoin(sourceData, profile.FieldsMapping);

                    hasRecord = listTarget.Any();

                    if (hasRecord)
                    {
                        List<Task> processTasks = new();

                        foreach (var targetData in listTarget)
                        {
                            processTasks.Add(operation.ProcessDataAsync(repository, new List<(EntityType entityType, string id, JObject data)>
                                {
                                    new (EntityType.Source, sourceData.Key, sourceData.Value),
                                    new (EntityType.Target, targetData.SelectToken("id").ToString(), targetData)
                                }, profile, job));
                        }

                        await Task.WhenAll(processTasks);

                        if (profile.OperationType != OperationType.Delete)
                        {
                            skip += take;
                            job.TargetProcessed = skip;
                            await _jobService.UpdateJob(job);
                        }
                    }

                    hasRecord = listTarget.Count == take; // if the list of Record is less than the take, it means that there will no have any more data to be checked.

                } while (hasRecord);
            }

            job.TargetProcessed = 0;
            int sourceSkip = job.SourceProcessed;

            sourceSkip++;
            job.SourceProcessed = sourceSkip;

            await _jobService.UpdateJob(job);
        }
    }
}