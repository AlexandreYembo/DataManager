using Connectors.Redis;
using Migration.Core;
using Migration.EventHandlers.Publishers;
using Migration.Models;
using Migration.Models.Logs;
using Migration.Models.Profile;
using Newtonsoft.Json.Linq;

namespace Migration.Services.Operations.OperationsByType
{
    public class DeleteSourceData : OperationBase, IOperation
    {
        private readonly LogDetailsPublisher _logDetailsPublisher;

        public DeleteSourceData(
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

            backup.Add("id", sourceData.SelectToken("id").ToString());
            backup.Add("Deleted", sourceData);

            await SaveCopyInLocal(profile.Source.Settings.CurrentEntity.Name, backup, job);

            logDetails.Descriptions.Add(new("Creating copy of the original data"));

            RepositoryParameters repositoryParameters = new()
            {
                Data = sourceData,
                FieldMappings = profile.FieldsMapping
            };

            await repository.DeleteAsync(repositoryParameters);
            logDetails.Descriptions.Add(new("Record deleted"));

            await _logDetailsPublisher.PublishAsync(logDetails);
        }
    }
}