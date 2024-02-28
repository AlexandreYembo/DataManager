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
    /// <summary>
    /// Process only Insert operation to the Database for the Target Table
    /// </summary>
    public class InsertTargetData : OperationBase, IOperation
    {
        private readonly LogDetailsPublisher _logDetailsPublisher;

        public InsertTargetData(
            IRepository<JObject> migrationProcessRepository,
            LogDetailsPublisher logDetailsPublisher,
            IJobService jobService) : base(migrationProcessRepository, jobService)
        {
            _logDetailsPublisher = logDetailsPublisher;
        }

        public async Task ProcessDataAsync(IGenericRepository repository, List<(EntityType entityType, string id, JObject data)> data, ProfileConfiguration profile, Jobs job)
        {
            var targetData = new JObject();
            var sourceData = data.FirstOrDefault(f => f.entityType == EntityType.Source).data;

            string id = Guid.NewGuid().ToString();

            LogDetails logDetails = new()
            {
                Display = true,
                Title = id,
                JobId = job.JobId,
                OperationType = profile.OperationType
            };

            JObject backup = new JObject();

            bool hasChange = false;

            var propertyId = profile.Target.Settings.CurrentEntity.Attributes.FirstOrDefault(f => f.Key == "RecordId").Value; //ableAttributesType.RecordId.ToString()).Value;
            targetData[propertyId] = id;

            var objectToBeImported = UpdateDataHelper.UpdateObject(targetData.ToString(), profile.FieldsMapping, sourceData, ref hasChange);

            if (!hasChange) return;

            backup.Add("id", objectToBeImported.SelectToken(propertyId).ToString());
            backup.Add("Inserted", objectToBeImported);
            await SaveCopyInLocal(profile.Target.Settings.CurrentEntity.Name, backup, job);

            RepositoryParameters repositoryParameters = new()
            {
                Data = objectToBeImported,
                FieldMappings = profile.FieldsMapping
            };

            await repository.InsertAsync(repositoryParameters);

            logDetails.Descriptions.Add("Record imported");

            await _logDetailsPublisher.PublishAsync(logDetails);
        }
    }
}