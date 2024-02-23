using Connectors.Redis;
using Migration.Core;
using Migration.EventHandlers.Publishers;
using Migration.Models;
using Migration.Models.Logs;
using Migration.Models.Profile;
using Migration.Services.Helpers;
using Newtonsoft.Json.Linq;

namespace Migration.Services.Operations.OperationsByType
{
    public class CacheTargetData : IOperation
    {
        private readonly LogDetailsPublisher _logDetailsPublisher;
        private readonly ICacheRepository _backupRepository;

        public CacheTargetData(
            ICacheRepository backupRepository,
            LogDetailsPublisher logDetailsPublisher)
        {
            _logDetailsPublisher = logDetailsPublisher;

            _backupRepository = backupRepository;
        }

        public async Task ProcessDataAsync(IGenericRepository repository, List<(EntityType entityType, string id, JObject data)> data, ProfileConfiguration profile, Jobs job)
        {
            var sourceData = data.FirstOrDefault(f => f.entityType == EntityType.Source).data;
            var targetData = data.FirstOrDefault(f => f.entityType == EntityType.Target).data;

            LogDetails logDetails = new()
            {
                Display = true,
                Title = sourceData.SelectToken("id").ToString(),
                JobId = job.JobId,
                OperationType = profile.OperationType
            };

            bool hasChange = false;

            var objectToBeUpdated = UpdateDataHelper.UpdateObject(targetData.ToString(), profile.FieldsMapping, sourceData, ref hasChange);

            if (!hasChange) return;

            logDetails.Descriptions.Add("Data exported to the Local Redis");

            RepositoryParameters rp = new()
            {
                Data = objectToBeUpdated,
                Entity = profile.Target.Settings.CurrentEntity.Name
            };

            await _backupRepository.InsertAsync(rp);

            await _logDetailsPublisher.PublishAsync(logDetails);
        }
    }
}