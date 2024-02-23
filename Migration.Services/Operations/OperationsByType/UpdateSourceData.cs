using Migration.Core;
using Migration.Models.Logs;
using Migration.Models;
using Migration.Models.Profile;
using Newtonsoft.Json.Linq;
using Migration.Services.Helpers;
using Migration.EventHandlers.Publishers;
using Connectors.Redis;

namespace Migration.Services.Operations.OperationsByType
{
    public class UpdateSourceData : OperationBase, IOperation
    {
        private readonly LogDetailsPublisher _logDetailsPublisher;

        public UpdateSourceData(
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


            logDetails.Descriptions.Add("Creating copy of the original data");

            bool hasChange = false;

            var objectToBeUpdated = UpdateDataHelper.UpdateObject(sourceData.ToString(), profile.FieldsMapping, ref hasChange);

            backup.Add("id", sourceData.SelectToken("id").ToString());
            backup.Add("Backup", sourceData);
            backup.Add("Updated", objectToBeUpdated);

            await SaveCopyInLocal(profile.Source.Settings.CurrentEntity.Name, backup, job);

            if (!hasChange) return;

            var differences = DifferenceHelper.FindDifferences(sourceData, objectToBeUpdated);

            if (differences.Any())
            {
                logDetails.Descriptions.Add(new("Values updated: " + string.Join(",",
                    differences.Select(s => s.PropertyName + " = " + s.Object2Value))));

                RepositoryParameters repositoryParameters = new()
                {
                    Data = objectToBeUpdated,
                    FieldMappings = profile.FieldsMapping
                };

                await repository.UpdateAsync(repositoryParameters);
            }
            else
            {
                logDetails.Descriptions.Add("There is no change for this record");
            }

            await _logDetailsPublisher.PublishAsync(logDetails);
        }
    }
}