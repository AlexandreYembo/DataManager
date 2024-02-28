using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage;
using Migration.Core;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using Connectors.Azure.TableStorage.Extensions;
using Migration.Core.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Migration.Models;

namespace Connectors.Azure.TableStorage.Repository
{
    public sealed class WindowsAzureGenericRepository : IGenericRepository
    {
        protected const string DefaultEndpointsProtocol = "https";

        private readonly CloudStorageAccount _storageAccount;
        private readonly CloudTableClient _tableClient;
        private readonly CloudTable _table;
        private readonly DataSettings _settings;
        private readonly TableRequestOptions requestOptions;

        private Task<TableQuerySegment<DynamicTableEntity>> segmentDownloadTask;

        private int currentEntityIndex;

        public WindowsAzureGenericRepository(DataSettings settings)
        {
            if (settings.IsCacheConnection) return; //when is cache it ignores the repo and uses the redis

            _settings = settings;

            _storageAccount = settings.Parameters.Any(p => p.Key == "Is Emulator" && p.Value == "True") ?
                CloudStorageAccount.DevelopmentStorageAccount :
                CloudStorageAccount.Parse(CreateConnectionString(settings));

            _tableClient = _storageAccount.CreateCloudTableClient();

            requestOptions = new TableRequestOptions()
            {
                RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(3), 3)
            };

            if (settings.CurrentEntity == null || string.IsNullOrEmpty(settings.CurrentEntity.Name))
            {
                settings.CurrentEntity = settings.Entities.FirstOrDefault();
            }

            _table = _tableClient.GetTableReference(settings.CurrentEntity.Name);
        }

        public Task<DataSettings> TestConnection()
        {
            _settings.Entities = new();

            var listTables = _tableClient.ListTablesSegmentedAsync(null).GetAwaiter().GetResult();

            foreach (var table in listTables)
            {
                _settings.Entities.Add(new(table.Name)
                {
                    Attributes = new()
                    {
                        new()
                        {
                            Key = Constants.PARTITION_KEY
                        },
                        new()
                        {
                            Key = Constants.ROW_KEY
                        }
                    }
                });
            }

            return Task.FromResult(_settings);
        }


        public async Task CreateTableAsync() => await _table.CreateIfNotExistsAsync();

        public async Task InsertAsync(RepositoryParameters parameters)
        {
            try
            {
                var entity = parameters.Data;
                var fieldMappings = parameters.FieldMappings;

                var tableStructure = GetTableStructure();

                var tableEntity = entity.ConvertToElasticTableEntity(tableStructure, fieldMappings);

                tableEntity.ETag = $"W/\"datetime'{DateTime.Now:yyyy-MM-ddTHH%3Amm%3Ass.fffZ}'\"";

                TableOperation replaceOperation = TableOperation.InsertOrMerge(tableEntity);

                var response = await _table.ExecuteAsync(replaceOperation);

                if (response.HttpStatusCode != 204)
                {
                    throw new TableOperationException(DbOperationErrorCodeConstants.ERROR_INSERT_OPERATION, JsonConvert.SerializeObject(response.Result));
                }
            }
            catch (Exception e)
            {
                throw new TableOperationException(DbOperationErrorCodeConstants.GENERIC_EXCEPTION, e.Message);
            }
        }

        public async Task UpdateAsync(RepositoryParameters parameters)
        {
            try
            {
                var entity = parameters.Data;
                var fieldMappings = parameters.FieldMappings;

                var tableStructure = GetTableStructure();

                var tableEntity = entity.ConvertToElasticTableEntity(tableStructure, fieldMappings);

                tableEntity.ETag = entity[Constants.E_TAG].ToString();

                TableOperation replaceOperation = TableOperation.Replace(tableEntity);

                var response = await _table.ExecuteAsync(replaceOperation);

                if (response.HttpStatusCode != 204)
                {
                    throw new TableOperationException(DbOperationErrorCodeConstants.ERROR_UPDATE_OPERATION, JsonConvert.SerializeObject(response.Result));
                }
            }
            catch (Exception e)
            {
                throw new TableOperationException(DbOperationErrorCodeConstants.GENERIC_EXCEPTION, e.Message);
            }
        }


        public async Task DeleteAsync(RepositoryParameters parameters)
        {
            try
            {
                var entity = parameters.Data;

                TableEntity tEntity = new TableEntity(entity[Constants.PARTITION_KEY].ToString(), entity[Constants.ROW_KEY].ToString())
                {
                    ETag = entity[Constants.E_TAG].ToString()
                };

                TableOperation deleteOperation = TableOperation.Delete(tEntity);
                var response = await _table.ExecuteAsync(deleteOperation);

                if (response.HttpStatusCode != 204)
                {
                    throw new TableOperationException(DbOperationErrorCodeConstants.ERROR_DELETE_OPERATION, JsonConvert.SerializeObject(response.Result));
                }
            }
            catch (Exception e)
            {
                throw new TableOperationException(DbOperationErrorCodeConstants.GENERIC_EXCEPTION, e.Message);
            }
        }

        public async Task<Dictionary<string, JObject>> GetAsync(string rawQuery)
        {
            Dictionary<string, JObject> dictionary = new();

            var query = _table.BuildCustomQuery(rawQuery);

            if (!string.IsNullOrEmpty(query))
                throw new TableOperationException(DbOperationErrorCodeConstants.QUERY_NOT_DEFINED, "this operation cannot proceed as there is no filter, there are risks in performance issue");

            var tableQuery = new TableQuery<DynamicTableEntity>().Where(query);

            var segment = await _table.ExecuteQuerySegmentedAsync(tableQuery, null);

            foreach (DynamicTableEntity entity in segment.Results)
            {
                // Access properties dynamically
                JObject jObject = new JObject();

                string partitionKey = entity.PartitionKey;

                jObject.Add("id", partitionKey);
                jObject.Add(Constants.PARTITION_KEY, entity.PartitionKey);
                jObject.Add(Constants.ROW_KEY, entity.RowKey);
                jObject.Add(Constants.E_TAG, entity.ETag);

                foreach (KeyValuePair<string, EntityProperty> property in entity.Properties)
                {
                    string propertyName = property.Key;
                    object propertyValue = property.Value.PropertyAsObject;

                    jObject.Add(propertyName, propertyValue.ToString());
                }

                var value = ((JValue)jObject["id"]).Value;
                dictionary[value.ToString()] = jObject;
            }

            return dictionary;
        }

        public async Task<Dictionary<string, JObject>> GetAsync(RepositoryParameters parameters)
        {
            var data = parameters.Data;
            var fieldMappings = parameters.FieldMappings;
            var take = parameters.Pagination.Take;
            var skip = parameters.Pagination.Skip;
            var rawQuery = parameters.Query;

            var cancellation = CancellationToken.None;

            currentEntityIndex = skip; //apply pagination to the next query to skip and go to the next items

            Dictionary<string, JObject> dictionary = new();

            var query = _table.BuildCustomQuery(rawQuery, fieldMappings, data, take);

            var currentSegment = await GetRecordByPage(query, take, cancellation);

            if (currentSegment == null)
            {
                segmentDownloadTask = null;
                return dictionary;
            }

            if (currentSegment.Results.Count == 0)
            {
                segmentDownloadTask = null;
            }

            // Access properties dynamically
            foreach (DynamicTableEntity entity in currentSegment.Results)
            {
                // Access properties dynamically
                JObject jObject = new JObject();

                string idRelationship = string.Empty;

                if (fieldMappings != null && fieldMappings.Any(w => w.SourceEntity == _settings.CurrentEntity.Name && w.MappingType == MappingType.TableJoin))
                {
                    idRelationship = fieldMappings.FirstOrDefault(w => w.SourceEntity == _settings.CurrentEntity.Name && w.MappingType == MappingType.TableJoin).SourceField;
                }
                else if  (fieldMappings != null && fieldMappings.Any(w => w.TargetEntity == _settings.CurrentEntity.Name && w.MappingType == MappingType.TableJoin))
                {
                    idRelationship = fieldMappings.FirstOrDefault(w => w.TargetEntity == _settings.CurrentEntity.Name && w.MappingType == MappingType.TableJoin).TargetField;
                }
                else
                {
                    idRelationship = _settings.CurrentEntity.Attributes.FirstOrDefault(w => w.Key == "RecordId").Value;
                }

                if(idRelationship == Constants.PARTITION_KEY)
                {
                    jObject.Add("id", entity.PartitionKey);
                }
                else if (idRelationship == Constants.ROW_KEY)
                {
                    jObject.Add("id", entity.RowKey);
                }
                else
                {
                    jObject.Add("id", $"{entity.Properties.FirstOrDefault(f => f.Key == idRelationship).Value.PropertyAsObject}");
                }
                jObject.Add(Constants.PARTITION_KEY, entity.PartitionKey);
                jObject.Add(Constants.ROW_KEY, entity.RowKey);
                jObject.Add(Constants.E_TAG, entity.ETag);

                foreach (KeyValuePair<string, EntityProperty> property in entity.Properties)
                {
                    string propertyName = property.Key;
                    object propertyValue = property.Value.PropertyAsObject;

                    jObject.Add(propertyName, propertyValue.ToString());
                }

                var value = ((JValue)jObject["id"]).Value;
                dictionary[value.ToString()] = jObject;
            }

            return dictionary;
        }


        private static string CreateConnectionString(DataSettings settings)
        {
            return $"{nameof(DefaultEndpointsProtocol)}={DefaultEndpointsProtocol};" +
                   $"AccountName={settings.GetAccountName()};" +
                   $"AccountKey={settings.GetAuthKey()};" +
                   "EndpointSuffix=core.windows.net;";
        }

        /// <summary>
        /// Recover the Structure of the table to make sure that the new data will have the same type
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, EdmType> GetTableStructure()
        {
            Dictionary<string, EdmType> properties = new();
            TableQuery<DynamicTableEntity> projectionQuery = new TableQuery<DynamicTableEntity>().Take(1);

            TableQuerySegment<DynamicTableEntity> queryResult = _table.ExecuteQuerySegmentedAsync(projectionQuery, null).GetAwaiter().GetResult();

            foreach (DynamicTableEntity entity in queryResult)
            {
                foreach (KeyValuePair<string, EntityProperty> property in entity.Properties)
                {
                    string propertyName = property.Key;
                    var type = property.Value.PropertyType;
                    properties.Add(propertyName, type);
                }
            }

            return properties;
        }

        #region Query Pagination

        /// <summary>
        /// Method that resolves the issue with Paginations in Table Storage
        /// the First query stores the segment that has the property 'TableContinuationToken' which means the next time the query is called, the task will be recovered and the next items of the pagination will be taken
        /// this is important: currentSegment.ContinuationToken for the pagination
        /// </summary>
        /// <param name="query"></param>
        /// <param name="take"></param>
        /// <param name="cancellation"></param>
        /// <returns></returns>
        private async Task<TableQuerySegment<DynamicTableEntity>?> GetRecordByPage(string query, int take, CancellationToken cancellation)
        {
            var tableQuery = new TableQuery<DynamicTableEntity>().Where(query).Take(take);

            if (segmentDownloadTask == null)
            {
                MoveToNextSegment(tableQuery, null, cancellation);
            }

            var currentSegment = await segmentDownloadTask;

            // Make sure current segment has data to read
            while (currentEntityIndex >= currentSegment.Results.Count && currentSegment.ContinuationToken != null)
            {
                MoveToNextSegment(tableQuery, currentSegment.ContinuationToken, cancellation);
                currentSegment = await segmentDownloadTask;
                currentEntityIndex = 0;
            }

            if (currentEntityIndex >= currentSegment.Results.Count && currentSegment.ContinuationToken == null)
            {
                return null;
            }

            return currentSegment;
        }

        private void MoveToNextSegment(TableQuery<DynamicTableEntity> query, TableContinuationToken continuationToken, CancellationToken cancellation)
        {
            segmentDownloadTask = _table.ExecuteQuerySegmentedAsync(query: query, token: continuationToken, requestOptions: requestOptions, operationContext: null, cancellationToken: cancellation);
        }

        #endregion
    }
}