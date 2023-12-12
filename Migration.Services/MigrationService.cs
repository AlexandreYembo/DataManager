using Migration.Infrastructure.Redis;
using Migration.Infrastructure.Redis.Entities;
using Migration.Repository;
using Migration.Repository.Extensions;
using Migration.Repository.Models;
using Migration.Services.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StackExchange.Redis;

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

        public MigrationService(Func<DataSettings, IGenericRepository> genericRepository,
            IRepository<JObject> migrationProcessRepository)
        {
            _genericRepository = genericRepository;
            _migrationProcessRepository = migrationProcessRepository;
        }

        public async Task Migrate(DataMapping dataMapping)
        {
            List<Task> processTasks = new();

            //TODO: Obtain records already processed (can use pagination)
            var source = await _genericRepository(dataMapping.Source.Settings)
                .Get(dataMapping.Source.Query, null, null, 5);

            if (source.Any())
            {
                foreach (var sourceData in source)
                {
                    processTasks.Add(ProcessDestinationRecordsAsync(dataMapping, sourceData));
                }

                //TODO: Subscribe an event to add the records already processed, can save pagination OFFSET <offset_amount> LIMIT <limit_amount>
                //https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query/offset-limit

                await Task.WhenAll(processTasks);
            }
        }

        private async Task ProcessDestinationRecordsAsync(DataMapping dataMapping,
            KeyValuePair<string, string> sourceData)
        {
            var destination = await _genericRepository(dataMapping.Destination.Settings)
                .Get(dataMapping.Destination.Query, dataMapping.FieldsMapping, sourceData.Value, 15);

            var listDestination = destination.ApplyJoin(sourceData, dataMapping.FieldsMapping);

            if (!listDestination.Any()) return;

            var sourceObj = JObject.Parse(sourceData.Value);

            foreach (var originalData in listDestination)
            {
                try
                {
                    bool hasChange = false;

                    var objectToBeUpdated = UpdateDataHelper.UpdateObject(originalData.ToString(), dataMapping.FieldsMapping,
                        sourceObj, ref hasChange);

                    if (!hasChange) return;

                    await Task.Run(() => SaveBackup(dataMapping, originalData, "backup"));
                    await Task.Run(() => SaveBackup(dataMapping, objectToBeUpdated, "updated"));
                }
                catch (Exception e)
                {
                    //await _migrationProcessRepository.SaveAsync(new RedisData<List<string>>()
                    //{
                    //    Data =
                    //})
                }
            }
        }

        private async Task SaveBackup(DataMapping dataMapping, JObject data, string suffix)
        {
            var redisData = new RedisData<JObject>()
            {
                Key = $"{data["id"]}-{suffix}",
                Data = data
            };

            await _migrationProcessRepository.SaveAsync(redisData, $"{dataMapping.Destination.Settings.CurrentEntity}-Migration");
        }
    }
}