using Connectors.Redis;
using Migration.Core;
using Migration.EventHandlers.Publishers;
using Migration.Models.Logs;
using Migration.Models.Profile;
using Migration.Models;
using Migration.Services.Helpers;
using Newtonsoft.Json.Linq;

namespace Migration.Services.Operations.OperationsByType
{
    public class ReportTargetData : OperationBase, IOperation
    {
        private readonly LogDetailsPublisher _logDetailsPublisher;

        public ReportTargetData(
            IRepository<JObject> migrationProcessRepository,
            LogDetailsPublisher logDetailsPublisher,
            IJobService jobService) : base(migrationProcessRepository, jobService)
        {
            _logDetailsPublisher = logDetailsPublisher;
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

            JObject backup = new JObject();

            bool hasChange = false;

            var objectToBeUpdated = UpdateDataHelper.UpdateObject(targetData.ToString(), profile.FieldsMapping, sourceData, ref hasChange);

            var id = profile.FieldsMapping.FirstOrDefault(f => f.MappingType == MappingType.TableJoin).TargetField;

            if (!hasChange) return;

            backup.Add("id", targetData.SelectToken(id).ToString());
            backup.Add("OriginalData", targetData);
            backup.Add("Report", objectToBeUpdated);

            await SaveCopyInLocal(profile.Target.Settings.CurrentEntity.Name, backup, job);

            logDetails.Descriptions.Add("Creating report data");

            await _logDetailsPublisher.PublishAsync(logDetails);
        }
    }
}