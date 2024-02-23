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
    public class CacheSourceData: OperationBase, IOperation
    {
        private readonly LogDetailsPublisher _logDetailsPublisher;

    public CacheSourceData(
        IRepository<JObject> migrationProcessRepository,
        LogDetailsPublisher logDetailsPublisher,
        IJobService jobService) : base(migrationProcessRepository, jobService)
    {
        _logDetailsPublisher = logDetailsPublisher;
    }

        public async Task ProcessDataAsync(IGenericRepository repository, List<(EntityType entityType, string id, JObject data)> data, ProfileConfiguration profile, Jobs job)
        {
            var sourceData = data.FirstOrDefault(f => f.entityType == EntityType.Source).data;

            LogDetails logDetails = new()
            {
                Display = true,
                Title = sourceData.SelectToken("id").ToString(),
                JobId = job.JobId,
                OperationType = profile.OperationType
            };

            JObject backup = new JObject();

            bool hasChange = false;

            var objectToBeUpdated = UpdateDataHelper.UpdateObject(sourceData.ToString(), profile.FieldsMapping, ref hasChange);

            if (!hasChange) return;

            backup.Add("id", sourceData.SelectToken("id").ToString());
            backup.Add("Report", objectToBeUpdated);
           
            await SaveCopyInLocal(profile.Source.Settings.CurrentEntity.Name, backup, job);
     
            await _logDetailsPublisher.PublishAsync(logDetails);
        }
    }
}