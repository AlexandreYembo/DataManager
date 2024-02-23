using Connectors.Redis;
using Migration.Core;
using Migration.EventHandlers.Publishers;
using Migration.Models;
using Migration.Models.Logs;
using Migration.Models.Profile;
using Newtonsoft.Json.Linq;

namespace Migration.Services.Operations.OperationsByType
{
    /// <summary>
    /// Process only Delete operation to the Database for the Target Table
    /// </summary>
    public class DeleteTargetData : OperationBase, IOperation
    {
        private readonly LogDetailsPublisher _logDetailsPublisher;

        public DeleteTargetData(
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

            logDetails.Descriptions.Add(new("Creating copy of the original data"));

            backup.Add("id", targetData["id"].ToString());
            backup.Add("Deleted", targetData);

            await SaveCopyInLocal(profile.Target.Settings.CurrentEntity.Name, backup, job);

            RepositoryParameters repositoryParameters = new()
            {
                Data = targetData,
                FieldMappings = profile.FieldsMapping
            };

            logDetails.Descriptions.Add("Record deleted");

            await repository.DeleteAsync(repositoryParameters);

            await _logDetailsPublisher.PublishAsync(logDetails);
        }
    }
}