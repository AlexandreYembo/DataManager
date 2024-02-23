using Migration.Core;
using Migration.Models.Profile;
using Migration.Models;
using Newtonsoft.Json.Linq;
using Migration.Models.Logs;
using Migration.Services.Helpers;
using Migration.EventHandlers.Publishers;
using Connectors.Redis;

namespace Migration.Services.Operations.OperationsByType
{
    /// <summary>
    /// Process only Update operation to the Database for the Target Table
    /// </summary>

    public class UpdateTargetData : OperationBase, IOperation
    {
        private readonly LogDetailsPublisher _logDetailsPublisher;

        public UpdateTargetData(
            IRepository<JObject> migrationProcessRepository,
            LogDetailsPublisher logDetailsPublisher,
            IJobService jobService) : base(migrationProcessRepository, jobService)
        {
            _logDetailsPublisher = logDetailsPublisher;
        }

        public async Task ProcessDataAsync(IGenericRepository repository, List<(EntityType entityType, string id, JObject data)> data, ProfileConfiguration profile, Jobs job)
        {
            var targetData = data.FirstOrDefault(f => f.entityType == EntityType.Target).data;
            var sourceData = data.FirstOrDefault(f => f.entityType == EntityType.Source).data;

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

            logDetails.Descriptions.Add("Creating copy of the original data");

            bool hasChange = false;

            var objectToBeUpdated = UpdateDataHelper.UpdateObject(targetData.ToString(), profile.FieldsMapping, sourceData, ref hasChange);


            if (!hasChange) return;

            backup.Add("id", targetData["id"].ToString());
            backup.Add("Backup", targetData);
            backup.Add("Updated", objectToBeUpdated);

            await SaveCopyInLocal(profile.Target.Settings.CurrentEntity.Name, backup, job);

            logDetails.Descriptions.Add("Creating copy of the changes");

            var differences = DifferenceHelper.FindDifferences(targetData, objectToBeUpdated);
            logDetails.Descriptions.Add("Values updated: " + string.Join(",", differences.Select(s => s.PropertyName + " = " + s.Object2Value)));

            RepositoryParameters repositoryParameters = new()
            {
                Data = objectToBeUpdated,
                FieldMappings = profile.FieldsMapping
            };

            await repository.UpdateAsync(repositoryParameters);

            await _logDetailsPublisher.PublishAsync(logDetails);
        }
    }
}