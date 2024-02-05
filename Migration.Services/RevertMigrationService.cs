using Migration.Repository;
using Migration.Repository.Delegates;
using Migration.Repository.LogModels;
using Migration.Repository.Models;
using Migration.Repository.Publishers;
using Migration.Services.Extensions;
using Migration.Services.Helpers;
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
            LogPublisher logPublisher,
            LogDetailsPublisher logDetailsPublisher,
            ActionsPublisher actionsPublisher)
        {
            _genericRepository = genericRepository;
            _logResultPublisher = logPublisher;
            _logDetailsPublisher = logDetailsPublisher;

            _actionsPublisher = actionsPublisher;
        }

        public async Task Revert(Profile profile, List<JObject> listData, int jobId)
        {
            LogResult log = new()
            {
                EntityName = profile.DataMappings[0].DataQueryMappingType == DataQueryMappingType.UpdateAnotherCollection ? profile.DataMappings[0].Destination.Settings.CurrentEntity.Name
                    : profile.DataMappings[0].Source.Settings.CurrentEntity.Name,
                StartedIn = DateTime.Now,
                Description = $"Reverting Migration: {profile.DataMappings[0].OperationType}.",
                JobId = jobId,
                OperationType = GetOperationTypeReverting(profile)
            };

            await _logResultPublisher.PublishAsync(log);

            var settings = GetDataSettings(profile);

            var repository = _genericRepository(settings);

            foreach (var data in listData)
            {
                var currentEntity = profile.DataMappings[0].Destination.Settings.CurrentEntity;
                var idField = currentEntity.Attributes.FirstOrDefault().Value.Replace("/", string.Empty);

                bool hasChange = false;
                var id = data.SelectToken("id").ToString();
                LogDetails logDetails = new()
                {
                    Display = true,
                    Title = id,
                    JobId = jobId,
                    OperationType = GetOperationTypeReverting(profile)
                };

                if (profile.DataMappings[0].OperationType == OperationType.Update)
                {
                    var backupData = data.SelectToken("Backup");
                    var updatedData = data.SelectToken("Updated");

                    if (backupData != null && updatedData != null)
                    {
                        var liveData = await repository.Get($"select * from c where c.{idField} = '{id}'");
                        var liveDataObject = JObject.Parse(liveData.Values.FirstOrDefault());
                        profile.DataMappings[0].FieldsMapping = profile.DataMappings[0].FieldsMapping.RevertMapping();

                        var revertValidationResult = await CheckDifferencesAllowToRevert(profile, jobId, liveDataObject, updatedData, logDetails, backupData, hasChange, 0);

                        if (!revertValidationResult.allowChange) continue;

                        await repository.Update(revertValidationResult.objectToBeUpdated, profile.DataMappings[0].FieldsMapping);
                        //update db
                    }
                }
                else if (profile.DataMappings[0].OperationType == OperationType.Delete)
                {
                    var deletedData = data.SelectToken("Deleted");

                    var liveData = await repository.Get($"select * from c where c.{idField} = '{id}'");

                    if (liveData.Any())
                    {
                        var liveDataObject = JObject.Parse(liveData.Values.FirstOrDefault());

                        await CheckDifferencesAllowToRevert(profile, jobId, liveDataObject, null, logDetails, deletedData, hasChange, 0);
                    }
                    else
                    {
                        await repository.Insert(JObject.FromObject(deletedData), profile.DataMappings[0].FieldsMapping);

                        logDetails.Descriptions.Add("Record inserted");

                        await _logDetailsPublisher.PublishAsync(logDetails);
                    }
                }
                else if (profile.DataMappings[0].OperationType == OperationType.Import)
                {
                    var insertedData = data.SelectToken("Inserted");

                    var liveData = await repository.Get($"select * from c where c.{idField} = '{id}'");

                    if (!liveData.Any())
                    {
                        logDetails.Descriptions.Add("There isn't any record to be deleted");

                        await _logDetailsPublisher.PublishAsync(logDetails);
                    }
                    else
                    {
                        await _logResultPublisher.PublishAsync(log);

                        var liveDataObject = JObject.Parse(liveData.Values.FirstOrDefault());

                        var allowDelete = await CheckDifferencesAllowToDelete(profile, jobId, liveDataObject, insertedData, logDetails, 0);

                        if (allowDelete)
                        {
                            await repository.Delete(liveDataObject);

                            logDetails.Descriptions.Add("Record deleted");

                            await _logDetailsPublisher.PublishAsync(logDetails);
                        }
                        else
                        {
                            logDetails.Descriptions.Add("There was another change on this data");
                        }
                    }
                }
            }

            log.FinishedIn = DateTime.Now;
            await _logResultPublisher.PublishAsync(log);

            await _actionsPublisher.PublishAsync(new Actions()
            {
                Message = "Reverting data completed. Please check the logs to see if there is anything to review!"
            });
        }

        private static OperationType GetOperationTypeReverting(Profile profile)
        {
            switch (profile.DataMappings[0].OperationType)
            {
                case OperationType.Delete:
                    return OperationType.Import;
                case OperationType.Import:
                    return OperationType.Delete;
                default:
                    return OperationType.Update;
            }

        }

        private async Task<(bool allowChange, JObject objectToBeUpdated)> CheckDifferencesAllowToRevert(Profile profile, int jobId, JObject liveDataObject, JToken? updatedData, LogDetails logDetails, JToken backupData, bool hasChange, int attempts)
        {
            var differencesBackupAndLiveData =
                DifferenceHelper.FindDifferences(liveDataObject, JObject.Parse(backupData.ToString()), false);
            if (!differencesBackupAndLiveData.Any())
            {
                logDetails.Descriptions.Add("Back and live data are the same, no need to revert changes for this record");

                logDetails.Type = LogType.Warn;
                logDetails.Display = true;
                await _logDetailsPublisher.PublishAsync(logDetails);

                return (false, null);
            }

            if (updatedData != null)
            {
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

                    await _logDetailsPublisher.PublishAsync(logDetails);
                    return (false, null);
                }
            }
            else
            {
                logDetails.Descriptions.Add("Values updated are not the same from live data. Please check: " +
                                            "Live Data = '" + string.Join(",",
                                                differencesBackupAndLiveData.Select(s =>
                                                    s.PropertyName + " = " + s.Object1Value)) + "'" +
                                            "Updated Data = '" + string.Join(",",
                                                differencesBackupAndLiveData.Select(s =>
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

                await _logDetailsPublisher.PublishAsync(logDetails);
                return (false, null);
            }


            var objectToBeUpdated = new JObject();

            if (profile.DataMappings[0].FieldsMapping.Any())
            {
                objectToBeUpdated = UpdateDataHelper.UpdateObject(liveDataObject.ToString(),
                    profile.DataMappings[0].FieldsMapping, JObject.Parse(backupData.ToString()), ref hasChange);
            }
            else
            {
                objectToBeUpdated = JObject.Parse(backupData.ToString());
                hasChange = true;
            }

            logDetails.Descriptions.Add("Values reverted to previous " + string.Join(",",
                differencesBackupAndLiveData.Select(s => s.PropertyName + " = " + s.Object2Value)));

            logDetails.Type = LogType.Success;
            logDetails.Display = true;

            await _logDetailsPublisher.PublishAsync(logDetails);

            return (hasChange, objectToBeUpdated);
        }


        private async Task<bool> CheckDifferencesAllowToDelete(Profile profile, int jobId, JObject liveDataObject, JToken insertedData, LogDetails logDetails, int attempts)
        {
            var differencesUpdatedAndLiveData =
                DifferenceHelper.FindDifferences(liveDataObject, JObject.Parse(insertedData.ToString()), false);

            if (differencesUpdatedAndLiveData
                .Any()) //It means that the version that has been updated during migration is already obsolete, but offers the option to Accept the conflict and update the record
            {

                logDetails.Descriptions.Add("Values are not the same from live data. Please check: " +
                                            "Live Data = '" + string.Join(",",
                                                differencesUpdatedAndLiveData.Select(s =>
                                                    s.PropertyName + " = " + s.Object1Value)) + "'" +
                                            "Inserted Data = '" + string.Join(",",
                                                differencesUpdatedAndLiveData.Select(s =>
                                                    s.PropertyName + " = " + s.Object2Value)) + "'");

                logDetails.Type = LogType.Error;
                logDetails.Display = true;
                logDetails.ActionsLogs = new List<ActionsLog>()
                {
                    new()
                    {
                        Label = "Accept Conflicts and Delete Record",
                        Action = async () =>
                            await VerifyAndAcceptToDeleteData(profile, JObject.Parse(insertedData.ToString()),
                                liveDataObject,
                                jobId, attempts++)
                    }
                };

                await _logDetailsPublisher.PublishAsync(logDetails);

                return false;
            }
            return true;
        }

        private static DataSettings GetDataSettings(Profile profile)
        {
            var settings = profile.DataMappings[0].DataQueryMappingType == DataQueryMappingType.UpdateSameCollection
                ? profile.DataMappings[0].Source.Settings
                : profile.DataMappings[0].Destination.Settings;
            return settings;
        }

        private async Task VerifyAndAcceptToRevertData(Profile profile, JObject backupData, JObject liveDataToCheck, int jobId, int attempts)
        {
            var currentEntity = profile.DataMappings[0].Destination.Settings.CurrentEntity;

            var idField = currentEntity.Attributes.FirstOrDefault().Value.Replace("/", string.Empty);

            var id = backupData.SelectToken(idField).ToString();
            LogDetails logDetails = new()
            {
                Display = true,
                Title = id,
                JobId = jobId,
                OperationType = GetOperationTypeReverting(profile)
            };

            var settings = GetDataSettings(profile);

            var repository = _genericRepository(settings);
            
            var liveData = await repository.Get($"select * from c where c.{idField} = '{id}'");
            var liveDataObject = JObject.Parse(liveData.Values.FirstOrDefault());

            bool hasChange = false;

            //Check again because if the user delays to resolve it and there was already another change the validation should be performed against

            var revertValidationResult = await CheckDifferencesAllowToRevert(profile, jobId, liveDataObject, liveDataToCheck, logDetails, backupData, hasChange, attempts);

            if (!revertValidationResult.allowChange)
            {
                await _actionsPublisher.PublishAsync(new Actions()
                {
                    Message = "There was another change on this data, please check again the logs updated"
                });

                return;
            }

            //update the data
            await repository.Update(revertValidationResult.objectToBeUpdated, profile.DataMappings[0].FieldsMapping);

            //update the db
            await _actionsPublisher.PublishAsync(new Actions()
            {
                Message = "Record updated"
            });
        }

        private async Task VerifyAndAcceptToDeleteData(Profile profile, JObject backupData, JObject liveDataToCheck, int jobId, int attempts)
        {
            var currentEntity = profile.DataMappings[0].Destination.Settings.CurrentEntity;
            var idField = currentEntity.Attributes.FirstOrDefault().Value.Replace("/", string.Empty);

            var id = backupData.SelectToken(idField).ToString();
            LogDetails logDetails = new()
            {
                Display = true,
                Title = id,
                JobId = jobId,
                OperationType = GetOperationTypeReverting(profile)
            };

            var settings = GetDataSettings(profile);

            var repository = _genericRepository(settings);


            var liveData = await repository.Get($"select * from c where c.{idField} = '{id}'");

            var liveDataObject = JObject.Parse(liveData.Values.FirstOrDefault());

            //Check again because if the user delays to resolve it and there was already another change the validation should be performed against
            var allowDelete = await CheckDifferencesAllowToDelete(profile, jobId, liveDataObject, liveDataToCheck, logDetails, attempts);


            if (!allowDelete)
            {
                await _actionsPublisher.PublishAsync(new Actions()
                {
                    Message = "There was another change on this data, please check again the logs updated"
                });

                return;
            }

            //update the data
            await repository.Delete(liveDataObject);

            //update the db

            var message = "Record deleted";
            await _actionsPublisher.PublishAsync(new Actions()
            {
                Message = message
            });

            logDetails.Descriptions.Add(message);

            await _logDetailsPublisher.PublishAsync(logDetails);
        }
    }
}