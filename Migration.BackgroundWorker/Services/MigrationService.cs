using Migration.BackgroundWorker.Queue;
using Migration.Infrastructure.Redis;
using Migration.Infrastructure.Redis.Entities;
using Migration.Repository;
using Migration.Repository.Extensions;
using Migration.Repository.Models;
using Newtonsoft.Json.Linq;

namespace Migration.BackgroundWorker.Services
{
    public class MigrationService : IMigrationService
    {
        private readonly Func<DataSettings, IGenericRepository> _genericRepository;
        private readonly IRepository<JObject> _repository;
        private readonly IRepository<Profile> _profileRepository;
        private readonly IBackgroundTaskQueue _taskQueue;
        
        public MigrationService(Func<DataSettings, IGenericRepository> genericRepository,
            IBackgroundTaskQueue taskQueue,
            IRepository<JObject> repository,
            IRepository<Profile> profileRepository)
        {
            _genericRepository = genericRepository;
            _repository = repository;
            _profileRepository = profileRepository;
            _taskQueue = taskQueue;
        }


        public async Task Process(string jobId)
        {
            var job = await GetJobId(jobId);

            if (job == null)
            {
                return;
            }

            var profile = await GetProfileConfiguration(job["ProfileId"].Value<string>());

            if (profile == null)
            {
                return;
            }

            var dataMapping = profile.DataMappings[0];

            bool hasRecord;
            int skip = 0;
            int take = 15;

            do
            {
                var repository = _genericRepository(dataMapping.Source.Settings);
                var source = await repository.Get(dataMapping.Source.Query, null, null, take, skip);

                hasRecord = source.Any();

                if (source.Any())
                {
                    if (dataMapping.DataQueryMappingType == DataQueryMappingType.UpdateAnotherCollection)
                    {
                        foreach (var sourceData in source)
                        {

                            // Enqueue a background work item
                            await _taskQueue.QueueBackgroundWorkItemAsync(ProcessData);

                          //  await ProcessDestinationRecordsAsync(dataMapping, sourceData, jobId));
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
                }
            } while (hasRecord);
        }

        private ValueTask ProcessData(CancellationToken cancellationToken, string jobId)
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

        private async Task<JObject?> GetJobId(string jobId)
        {
            try
            {
                var job = JObject.Parse(await _repository.FindByKeyAsync(new RedisData<JObject>
                {
                    Id = "Jobs",
                    Key = jobId
                }));
                return job;
            }
            catch
            {
                return null;
            }
        }

        private async Task<Profile?> GetProfileConfiguration(string profileId)
        {
            try
            {
                var profile = await _profileRepository.FindByKeyAsync(new RedisData<Profile>()
                {
                    Id = "Profile",
                    Key = profileId
                });

                return profile;
            }
            catch
            {
                return null;
            }
        }
    }
}