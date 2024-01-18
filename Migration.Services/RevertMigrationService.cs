using Migration.Infrastructure.Redis;
using Migration.Repository;
using Migration.Repository.Delegates;
using Migration.Repository.LogModels;
using Migration.Repository.Models;
using Migration.Repository.Publishers;
using Migration.Services.Extensions;
using Migration.Services.Helpers;
using Migration.Services.Subscribers;
using Newtonsoft.Json.Linq;

namespace Migration.Services
{
    public class RevertMigrationService : IRevertMigrationService
    {
        private readonly Func<DataSettings, IGenericRepository> _genericRepository;
        private readonly LogPublisher _logResultPublisher;
        private readonly LogDetailsPublisher _logDetailsPublisher;
        private readonly ActionsPublisher _actionsPublisher;

        public RevertMigrationService(Func<DataSettings, IGenericRepository> genericRepository,
            IRepository<JObject> migrationProcessRepository,
            LogPublisher logPublisher,
            LogDetailsPublisher logDetailsPublisher,
            ActionsPublisher actionsPublisher,
            MigrationLogPersistSubscriber migrationLogSubscriber)
        {
            _genericRepository = genericRepository;
            _logResultPublisher = logPublisher;
            _logDetailsPublisher = logDetailsPublisher;

            _logResultPublisher.OnEntityChanged += migrationLogSubscriber.LogResultPublisher_OnEntityChanged;
            _logDetailsPublisher.OnEntityChanged += migrationLogSubscriber.LogDetailsPublisher_OnEntityChanged;
            _actionsPublisher = actionsPublisher;
        }


        public async Task Revert(Profile profile, List<JObject> listData, int jobId)
        {
            if (profile.DataMappings[0].OperationType != OperationType.Update && profile.DataMappings[0].OperationType != OperationType.Delete)
            {
                return;
            }

            LogResult log = new()
            {
                EntityName = profile.DataMappings[0].DataQueryMappingType == DataQueryMappingType.UpdateAnotherCollection ? profile.DataMappings[0].Destination.Settings.CurrentEntity
                    : profile.DataMappings[0].Source.Settings.CurrentEntity,
                StartedIn = DateTime.Now,
                Description = $"Reverting Migration: {profile.DataMappings[0].OperationType}.",
                JobId = jobId
            };

            _logResultPublisher.Publish(log);

            var settings = GetDataSettings(profile);

            var repository = _genericRepository(settings);

            foreach (var data in listData)
            {
                bool hasChange = false;
                var id = data.SelectToken("id").ToString();
                LogDetails logDetails = new()
                {
                    Display = true,
                    Title = id,
                    JobId = jobId
                };

                if (profile.DataMappings[0].OperationType == OperationType.Update)
                {
                    var backupData = data.SelectToken("Backup");
                    var updatedData = data.SelectToken("Updated");

                    if (backupData != null && updatedData != null)
                    {
                        var liveData = await repository.Get($"select * from c where c.id = '{id}'");
                        var liveDataObject = JObject.Parse(liveData.Values.FirstOrDefault());
                        profile.DataMappings[0].FieldsMapping = profile.DataMappings[0].FieldsMapping.RevertMapping();

                        var revertValidationResult = CheckDifferencesAllowToRevert(profile, jobId, liveDataObject, updatedData, logDetails, backupData, hasChange, 0);

                        if (!revertValidationResult.allowChange) continue;

                        await repository.Update(revertValidationResult.objectToBeUpdated);
                        //update db
                    }
                }

                var deletedData = data.SelectToken("Deleted");
            }

            log.FinishedIn = DateTime.Now;
            _logResultPublisher.Publish(log);
        }

        private (bool allowChange, JObject objectToBeUpdated) CheckDifferencesAllowToRevert(Profile profile, int jobId, JObject liveDataObject, JToken updatedData,
            LogDetails logDetails, JToken backupData, bool hasChange, int attempts)
        {
            var differencesBackupAndLiveData =
                DifferenceHelper.FindDifferences(liveDataObject, JObject.Parse(backupData.ToString()), false, profile.DataMappings[0].FieldsMapping);
            if (!differencesBackupAndLiveData.Any())
            {
                logDetails.Descriptions.Add("Back and live data are the same, no need to revert changes for this record");
                logDetails.Type = LogType.Warn;
                logDetails.Display = true;
                _logDetailsPublisher.Publish(logDetails);

                return (false, null);
            }

            var differencesUpdatedAndLiveData =
                DifferenceHelper.FindDifferences(liveDataObject, JObject.Parse(updatedData.ToString()), false, profile.DataMappings[0].FieldsMapping);

            if (differencesUpdatedAndLiveData
                .Any()) //It means that the version that has been updated during migration is already obsolete, but offers the option to Accept the conflict and update the record
            {

                logDetails.Descriptions.Add("Values updated are not the same from live data. Please check: " +
                                            "Live Data = '" + string.Join(",",
                                                differencesUpdatedAndLiveData.Select(s =>
                                                    s.PropertyName + " = " + s.Object1Value)) + "'" +
                                            "Updated Data = '" + string.Join(",",
                                                differencesUpdatedAndLiveData.Select(s =>
                                                    s.PropertyName + " = " + s.Object2Value)) + "'");

                logDetails.Type = LogType.Error;
                logDetails.Display = true;
                logDetails.ActionsLogs = new List<ActionsLog>()
                {
                    new()
                    {
                        Label = "Accept Conflicts and Update Record",
                        Action = async () =>
                            await VerifyAndAcceptToRevertData(profile, JObject.Parse(backupData.ToString()),
                                liveDataObject,
                                jobId, attempts++)
                    }
                };

                _logDetailsPublisher.Publish(logDetails);
                return (false, null);
            }

            var objectToBeUpdated = UpdateDataHelper.UpdateObject(liveDataObject.ToString(),
                profile.DataMappings[0].FieldsMapping, JObject.Parse(backupData.ToString()), ref hasChange);

            logDetails.Descriptions.Add("Values reverted to previous " + string.Join(",",
                differencesBackupAndLiveData.Select(s => s.PropertyName + " = " + s.Object2Value)));

            logDetails.Type = LogType.Success;
            logDetails.Display = true;
            _logDetailsPublisher.Publish(logDetails);

            return (hasChange, objectToBeUpdated);
        }

        private static DataSettings GetDataSettings(Profile profile)
        {
            var settings = profile.DataMappings[0].DataQueryMappingType == DataQueryMappingType.UpdateSameCollection
                ? profile.DataMappings[0].Source.Settings
                : profile.DataMappings[0].Destination.Settings;
            return settings;
        }

        public async Task VerifyAndAcceptToRevertData(Profile profile, JObject backupData, JObject liveDataToCheck, int jobId, int attempts)
        {
            var id = backupData.SelectToken("id").ToString();
            LogDetails logDetails = new()
            {
                Display = true,
                Title = id,
                JobId = jobId
            };

            var settings = GetDataSettings(profile);

            var repository = _genericRepository(settings);

            var liveData = await repository.Get($"select * from c where c.id = '{backupData["id"]}'");
            var liveDataObject = JObject.Parse(liveData.Values.FirstOrDefault());

            bool hasChange = false;

            //Check again because if the user delays to resolve it and there was already another change the validation should be performed against

            var revertValidationResult = CheckDifferencesAllowToRevert(profile, jobId, liveDataObject, liveDataToCheck, logDetails, backupData, hasChange, attempts);

            if (!revertValidationResult.allowChange)
            {
                await _actionsPublisher.PublishAsync(new Actions()
                {
                    Message = "There was another change on this data, please check again the logs updated"
                });

                return;
            }

            //update the data
            await repository.Update(revertValidationResult.objectToBeUpdated);

            //update the db
            await _actionsPublisher.PublishAsync(new Actions()
            {
                Message = "Record updated"
            });
        }
    }
}