using Migration.Repository;
using Migration.Repository.DbOperations;
using Migration.Repository.Exceptions;
using Migration.Repository.Helpers;
using Migration.Repository.LogModels;
using Migration.Repository.Publishers;
using Migration.Services.Extensions;
using Migration.Services.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Services
{
    public interface IUpdateRecordsInBatchService
    {
        Task<List<DynamicData>> GetData(DBSettings settings, string query);

        Dictionary<string, List<JObject>> Preview(List<CommandModel> listCommands, List<DynamicData> data);

        Task<List<DynamicData>> Update(DBSettings settings, List<CommandModel> listCommands, List<DynamicData> data);

        Task<DynamicData> Revert(DBSettings settings, string recordId);
    }

    public class UpdateRecordsInBatchService : IUpdateRecordsInBatchService
    {
        private readonly Func<DBSettings, IGenericRepository> _genericRepository;
        private readonly LogPublisher _logResultPublisher;
        private readonly LogDetailsPublisher _logDetailsPublisher;
        private readonly IStorage<DynamicData> _storage;


        public UpdateRecordsInBatchService(
            Func<DBSettings, IGenericRepository> genericRepository,
            LogPublisher logResultPublisher,
            LogDetailsPublisher logDetailsPublisher,
            IStorage<DynamicData> storage)
        {
            _genericRepository = genericRepository;
            _logResultPublisher = logResultPublisher;
            _logDetailsPublisher = logDetailsPublisher;
            _storage = storage;
        }

        public async Task<List<DynamicData>> GetData(DBSettings settings, string query)
        {
            LogResult log = new()
            {
                EntityName = settings.Container,
                Description = $"Getting Data from {settings.Container}",
                StartedIn = DateTime.Now
            };

            _logResultPublisher.Publish(log);

            //Build the Repository(e.g. Cosmos Db or Table Storage
            var repositoryInstance = _genericRepository(settings);

            try
            {
                var dataResult = await repositoryInstance.Get(query);

                var result = dataResult.ToDynamicDataList();


                _logDetailsPublisher.Publish(new LogDetails()
                {
                    Display = true,
                    Descriptions = new ()
                    {
                       new($"{settings.Container} - {result} records obtained")
                    },
                    Title = $"{result} Records obtained",
                    Type = LogType.Success,
                }); log.FinishedIn = DateTime.Now;
                _logResultPublisher.Publish(log);

                return result;
            }
            catch (Exception e)
            {
                _logDetailsPublisher.Publish(new LogDetails()
                {
                    Display = true,
                    Descriptions = new ()
                    {
                        new(e.Message)
                    },
                    Title = "Error to get the record",
                    Type = LogType.Error,
                });

                log.FinishedIn = DateTime.Now;
                _logResultPublisher.Publish(log);

                return new();
            }
        }

        public Dictionary<string, List<JObject>> Preview(
            List<CommandModel> listCommands,
            List<DynamicData> data)
        {
            var commands = listCommands.Select(s => MapFieldTypes.BuildCommandDictionary(s));

            var result = new Dictionary<string, List<JObject>>();

            foreach (var entity in data.Select(s => s))
            {
                //Create 2 objects, one to persist original data, and one that will receive the changes
                JObject objectToBeUpdated = JObject.Parse(entity.Data);
                JObject originalData = JObject.Parse(entity.Data);

                var id = ((JValue)objectToBeUpdated["id"]);

                bool hasChange = false;

                foreach (var command in commands.SelectMany(s => s))
                {
                    var fieldsArr = command.Key.Split(".").ToList();

                    //Apply the change to the current property
                    objectToBeUpdated = JObjectHelper.GetObject(objectToBeUpdated, fieldsArr, command.Value);

                    hasChange = true;
                }

                if (!hasChange) continue;

                result.Add(id.ToString(), new List<JObject>() { originalData, objectToBeUpdated });
            }

            return result;
        }

        public async Task<List<DynamicData>> Update(DBSettings settings, List<CommandModel> listCommands, List<DynamicData> data)
        {
            var commands = listCommands.Select(s => MapFieldTypes.BuildCommandDictionary(s));

            var repositoryInstance = _genericRepository(settings);

            foreach (var entity in data.Select(s => s))
            {
                JObject objectToBeUpdated = JObject.Parse(entity.Data);

                bool hasChange = false;

                foreach (var command in commands.SelectMany(s => s))
                {
                    var fieldsArr = command.Key.Split(".").ToList();

                    objectToBeUpdated = JObjectHelper.GetObject(objectToBeUpdated, fieldsArr, command.Value);

                    hasChange = true;
                }

                if (!hasChange)
                {
                    _logDetailsPublisher.Publish(new LogDetails()
                    {
                        Display = true,
                        Descriptions = new()
                        {
                            new("Record skip")
                        },
                        Title = $"Skip update {entity.Id}",
                        Type = LogType.Warn,
                    });
                }
                else
                {
                    _logDetailsPublisher.Publish(new LogDetails()
                    {
                        Display = true,
                        Descriptions = new()
                        {
                           new("Record updated")
                        },
                        Title = $"Record updated {entity.Id}",
                        Type = LogType.Success,
                    });
                }

                try
                {
                    await repositoryInstance.Update(objectToBeUpdated);
                }
                catch (DbOperationException e)
                {
                    _logDetailsPublisher.Publish(new LogDetails()
                    {
                        Display = true,
                        Descriptions = new()
                        {
                            new(e.ErrorCode + e.ErrorMessage)
                        },
                        Title = $"Error to update the record {entity.Id}",
                        Type = LogType.Error,
                    });
                }

                await _storage.Add(entity, $"{settings.Container}-{entity.Id}");

                entity.Data = objectToBeUpdated.ToString();

                entity.Actions.Add(ActionType.RevertToPreviousChange);
            }

            return data;
        }

        public async Task<DynamicData> Revert(DBSettings settings, string recordId)
        {
            var originalData = await _storage.GetByKey($"{settings.Container}-{recordId}");

            //call update from repository

            var repositoryInstance = _genericRepository(settings);
            var entity = JObject.Parse(originalData.Data);

            await repositoryInstance.Update(entity);

            return originalData;
        }
    }
}