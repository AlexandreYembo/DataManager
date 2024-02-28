using Migration.Core;
using Migration.EventHandlers.Publishers;
using Migration.Models;
using Migration.Models.Logs;
using Migration.Models.Profile;
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

        public async Task Revert(ProfileConfiguration profile, List<JObject> listData, int jobId)
        {
            LogResult log = new()
            {
                EntityName = profile.DataQueryMappingType == DataQueryMappingType.SourceToTarget ? profile.Target.Settings.CurrentEntity.Name
                    : profile.Source.Settings.CurrentEntity.Name,
                StartedIn = DateTime.Now,
                Description = $"Reverting Migration: {profile.OperationType}.",
                JobId = jobId,
                OperationType = GetOperationTypeReverting(profile)
            };

            await _logResultPublisher.PublishAsync(log);

            try
            {
                var settings = GetDataSettings(profile);

                var repository = _genericRepository(settings);

                foreach (var data in listData)
                {
                    var currentEntity = profile.Target.Settings.CurrentEntity;
                    var idField = currentEntity.Attributes.Any(f => f.Key == "RecordId")
                        ? currentEntity.Attributes.FirstOrDefault(f => f.Key == "RecordId").Value
                        : currentEntity.Attributes.FirstOrDefault().Value.Replace("/", string.Empty);

                    bool hasChange = false;
                    var id = data.SelectToken("id").ToString();
                    LogDetails logDetails = new()
                    {
                        Display = true,
                        Title = id,
                        JobId = jobId,
                        OperationType = GetOperationTypeReverting(profile)
                    };

                    if (profile.OperationType == OperationType.Update)
                    {
                        var backupData = data.SelectToken("Backup");
                        var updatedData = data.SelectToken("Updated");

                        if (backupData != null && updatedData != null)
                        {
                            var liveData = await repository.GetAsync($"select * from c where c.{idField} = '{id}'");

                            if (!liveData.Any())
                            {
                                logDetails.Descriptions.Add($"Record not found for {idField}='{id}', skipping");

                                await _logDetailsPublisher.PublishAsync(logDetails);
                                continue;
                            }

                            var liveDataObject = liveData.Values.FirstOrDefault();
                            profile.FieldsMapping = profile.FieldsMapping.RevertMapping();

                            var revertValidationResult = await CheckDifferencesAllowToRevert(profile, jobId, liveDataObject, updatedData, logDetails, backupData, hasChange, 0);

                            if (!revertValidationResult.allowChange) continue;

                            RepositoryParameters repositoryParameter = new()
                            {
                                Data = revertValidationResult.objectToBeUpdated,
                                FieldMappings = profile.FieldsMapping
                            };

                            //update db
                            await repository.UpdateAsync(repositoryParameter);
                        }
                    }
                    else if (profile.OperationType == OperationType.Delete)
                    {
                        var deletedData = data.SelectToken("Deleted");

                        var liveData = await repository.GetAsync($"select * from c where c.{idField} = '{id}'");

                        if (liveData.Any())
                        {
                            var liveDataObject = liveData.Values.FirstOrDefault();

                            await CheckDifferencesAllowToRevert(profile, jobId, liveDataObject, null, logDetails, deletedData, hasChange, 0);
                        }
                        else
                        {
                            RepositoryParameters repositoryParameter = new()
                            {
                                Data = JObject.FromObject(deletedData),
                                FieldMappings = profile.FieldsMapping
                            };

                            await repository.InsertAsync(repositoryParameter);

                            logDetails.Descriptions.Add("Record inserted");

                            await _logDetailsPublisher.PublishAsync(logDetails);
                        }
                    }
                    else if (profile.OperationType == OperationType.Import)
                    {
                        var insertedData = data.SelectToken("Inserted");

                        var liveData = await repository.GetAsync($"select * from c where c.{idField} = '{id}'");

                        if (!liveData.Any())
                        {
                            logDetails.Descriptions.Add("There isn't any record to be deleted");

                            await _logDetailsPublisher.PublishAsync(logDetails);
                        }
                        else
                        {
                            await _logResultPublisher.PublishAsync(log);

                            foreach (var liveDataObject in liveData.Values)
                            {
                                var allowDelete = await CheckDifferencesAllowToDelete(profile, jobId, liveDataObject, insertedData, logDetails, 0);

                                logDetails.Title = liveDataObject.SelectToken("id").ToString();

                                if (allowDelete)
                                {
                                    RepositoryParameters repositoryParameter = new()
                                    {
                                        Data = JObject.FromObject(liveDataObject)
                                    };

                                    await repository.DeleteAsync(repositoryParameter);

                                    logDetails.Descriptions.Add("Record deleted");

                                    await _logDetailsPublisher.PublishAsync(logDetails);
                                }
                                else
                                {
                                    logDetails.Descriptions.Add("There was another change on this data");
                                    await _logDetailsPublisher.PublishAsync(logDetails);
                                }
                            }
                        }
                    }
                }

                await _actionsPublisher.PublishAsync(new Actions()
                {
                    Message = "Reverting data completed. Please check the logs to see if there is anything to review!"
                });
            }
            catch (Exception ex)
            {
                await _actionsPublisher.PublishAsync(new Actions()
                {
                    Message = "Error to revert the previous version of the data, check the logs for more details!"
                });

                LogDetails detailsError = new()
                {
                    LogDateTime = DateTime.Now,
                    Title = "Error Migration",
                    Descriptions = new()
                    {
                        ex.Message
                    },
                    Display = true,
                    JobId = jobId,
                    Type = LogType.Error,
                    OperationType = profile.OperationType
                };

                await _logDetailsPublisher.PublishAsync(detailsError);
            }

            log.FinishedIn = DateTime.Now;
            await _logResultPublisher.PublishAsync(log);
        }

        private static OperationType GetOperationTypeReverting(ProfileConfiguration profile)
        {
            switch (profile.OperationType)
            {
                case OperationType.Delete:
                    return OperationType.Import;
                case OperationType.Import:
                    return OperationType.Delete;
                default:
                    return OperationType.Update;
            }

        }

        private async Task<(bool allowChange, JObject objectToBeUpdated)> CheckDifferencesAllowToRevert(ProfileConfiguration profile, int jobId, JObject liveDataObject, JToken? updatedData, LogDetails logDetails, JToken backupData, bool hasChange, int attempts)
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
                    DifferenceHelper.FindDifferences(liveDataObject, JObject.Parse(updatedData.ToString()), false, profile.FieldsMapping);

                if (!differencesUpdatedAndLiveData.Any()) // If there is no difference, double check just to make sure that there will have nothing updated in live prod
                {
                    foreach (var fieldMapping in profile.FieldsMapping.Where(w => liveDataObject.SelectTokens(w.SourceField).Any()))
                    {
                        var value1 = liveDataObject.SelectToken(fieldMapping.SourceField).ToString();
                        var value2 = updatedData.SelectToken(fieldMapping.SourceField).ToString();

                        if (value1 != value2)
                        {
                            differencesUpdatedAndLiveData.Add(new Difference()
                            {
                                PropertyName = fieldMapping.SourceField,
                                Object1Value = value1 != null ? value1.ToString() : string.Empty,
                                Object2Value = value2 != null ? value2.ToString() : string.Empty
                            });
                        }
                    }
                }

                if (differencesUpdatedAndLiveData.Any()) //It means that the version that has been updated during migration is already obsolete, but offers the option to Accept the conflict and update the record
                {
                    var message = "Values from migration are not the same from live data. Please check: ";
                    foreach (var item in differencesUpdatedAndLiveData)
                    {
                        message += "<br> Live Data = '" + item.PropertyName + " = " + item.Object1Value + "'" +
                                  "<br> Data Migrated = '" + item.PropertyName + " = " + item.Object2Value + "'" +
                                  "<br>";
                    }

                    logDetails.Descriptions.Add(message);

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

            if (profile.FieldsMapping.Any())
            {
                objectToBeUpdated = UpdateObjectsBasedOnMappings(profile, liveDataObject, backupData, ref hasChange);

                //objectToBeUpdated = UpdateDataHelper.UpdateObject(liveDataObject.ToString(),profile.DataMappings[0].FieldsMapping, JObject.Parse(backupData.ToString()), ref hasChange);
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

        private static JObject UpdateObjectsBasedOnMappings(ProfileConfiguration profile, JObject liveDataObject, JToken backupData, ref bool hasChange)
        {
            var objectToBeUpdated = new JObject();

            foreach (var fieldMappings in profile.FieldsMapping)
            {
                var fieldsFromTargetArr = fieldMappings.TargetField.Split(".").ToList();

                var valueFromSource = JObjectHelper.GetValueFromObject(JObject.Parse(backupData.ToString()), fieldMappings.SourceField.Split(".").ToList());
                objectToBeUpdated = JObjectHelper.UpdateObjectFromOriginal(liveDataObject, JObject.Parse(backupData.ToString()), fieldsFromTargetArr, valueFromSource);
                hasChange = true;
            }

            return objectToBeUpdated;
        }

        private async Task<bool> CheckDifferencesAllowToDelete(ProfileConfiguration profile, int jobId, JObject liveDataObject, JToken insertedData, LogDetails logDetails, int attempts)
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

        private static DataSettings GetDataSettings(ProfileConfiguration profile)
        {
            var settings = profile.DataQueryMappingType == DataQueryMappingType.SameCollection
                ? profile.Source.Settings
                : profile.Target.Settings;
            return settings;
        }

        private async Task VerifyAndAcceptToRevertData(ProfileConfiguration profile, JObject backupData, JObject liveDataToCheck, int jobId, int attempts)
        {
            var currentEntity = profile.Target.Settings.CurrentEntity;

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

            var liveData = await repository.GetAsync($"select * from c where c.{idField} = '{id}'");
            var liveDataObject = liveData.Values.FirstOrDefault();

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

            RepositoryParameters repositoryParameters = new()
            {
                Data = revertValidationResult.objectToBeUpdated,
                FieldMappings = profile.FieldsMapping
            };

            //update the table
            await repository.UpdateAsync(repositoryParameters);

            await _actionsPublisher.PublishAsync(new Actions()
            {
                Message = "Record updated"
            });
        }

        private async Task VerifyAndAcceptToDeleteData(ProfileConfiguration profile, JObject backupData, JObject liveDataToCheck, int jobId, int attempts)
        {
            var currentEntity = profile.Target.Settings.CurrentEntity;
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


            var liveData = await repository.GetAsync($"select * from c where c.{idField} = '{id}'");

            var liveDataObject = liveData.Values.FirstOrDefault();

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

            RepositoryParameters repositoryParameters = new()
            {
                Data = liveDataObject
            };

            //update the data
            await repository.DeleteAsync(repositoryParameters);

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