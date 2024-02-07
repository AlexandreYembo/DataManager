using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Migration.Repository;
using Migration.Repository.Exceptions;
using Migration.Repository.Models;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Dynamic;
using System.Text;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
namespace Migration.Infrastructure.AzureTableStorage
{
    public class WindowsAzureGenericRepository : IGenericRepository
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
            if (!string.IsNullOrEmpty(settings.CurrentEntity.Name))
            {
                _settings = settings;

                _storageAccount = settings.Parameters.Any(p => p.Key == "Is Emulator" && p.Value == "True") ?
                    CloudStorageAccount.DevelopmentStorageAccount :
                    CloudStorageAccount.Parse(CreateConnectionString(settings));

                _tableClient = _storageAccount.CreateCloudTableClient();
                _table = _tableClient.GetTableReference(settings.CurrentEntity.Name);
            }

            requestOptions = new TableRequestOptions()
            {
                RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(3), 3)
            };
        }

        public async Task<Dictionary<string, string>> Get(string rawQuery)
        {
            Dictionary<string, string> dictionary = new();

            var query = QueryBuilder.Build(rawQuery, _table);

            if (!string.IsNullOrEmpty(query))
                throw new DbOperationException("this operation cannot proceed as there is no filter, there are risks in performance issue");

            var tableQuery = new TableQuery<DynamicTableEntity>().Where(query);

            var segment = await _table.ExecuteQuerySegmentedAsync(tableQuery, null);

            foreach (DynamicTableEntity entity in segment.Results)
            {
                // Access properties dynamically
                JObject jo = new JObject();

                string partitionKey = entity.PartitionKey;
                string rowKey = entity.RowKey;

                jo.Add("id", $"{partitionKey}");
                jo.Add("PartitionKey", partitionKey);
                jo.Add("RowKey", rowKey);
                jo.Add("ETag", entity.ETag);

                foreach (KeyValuePair<string, EntityProperty> property in entity.Properties)
                {
                    string propertyName = property.Key;
                    object propertyValue = property.Value.PropertyAsObject;

                    jo.Add(propertyName, propertyValue.ToString());
                }

                var value = ((JValue)jo["id"]).Value;
                dictionary[value.ToString()] = jo.ToString(); ;
            }

            return dictionary;
        }

        public async Task<Dictionary<string, string>> Get(string rawQuery, List<DataFieldsMapping> fieldMappings, string data, int take, int skip = 0)
        {
            var cancellation = CancellationToken.None;

            currentEntityIndex = skip; //apply pagination to the next query to skip and go to the next items

            Dictionary<string, string> dictionary = new();
            var query = QueryBuilder.Build(rawQuery, _table, fieldMappings, data, take);

            var currentSegment = await GetRecordByPage(query, take, cancellation);

            // Access properties dynamically
            foreach (DynamicTableEntity entity in currentSegment.Results)
            {
                // Access properties dynamically
                JObject jo = new JObject();

                string partitionKey = entity.PartitionKey;
                string rowKey = entity.RowKey;

                jo.Add("id", $"{partitionKey}");
                jo.Add("PartitionKey", partitionKey);
                jo.Add("RowKey", rowKey);
                jo.Add("ETag", entity.ETag);

                foreach (KeyValuePair<string, EntityProperty> property in entity.Properties)
                {
                    string propertyName = property.Key;
                    object propertyValue = property.Value.PropertyAsObject;

                    jo.Add(propertyName, propertyValue.ToString());
                }

                var value = ((JValue)jo["id"]).Value;
                dictionary[value.ToString()] = jo.ToString(); ;
            }

            return dictionary;
        }

        public async Task Update(JObject entity, List<DataFieldsMapping> fieldMappings = null)
        {
            try
            {
                var structure = GetTableStructure();

                if (!structure.ContainsKey("id"))
                {
                    entity.Remove("id"); // this is not used if is not part of the table schema
                }

                ElasticTableEntity tEntity = new ElasticTableEntity();

                tEntity.PartitionKey = entity["PartitionKey"].ToString();
                tEntity.RowKey = entity["RowKey"].ToString();
                tEntity.ETag = entity["ETag"].ToString();

                foreach (var e in entity)
                {
                    if (e.Key != "PartitionKey" && e.Key != "RowKey" && e.Key != "ETag")
                    {
                        EdmType fieldType;
                        var mappingType = fieldMappings?.Where(w => w.MappingType != MappingType.TableJoin).FirstOrDefault(a => a.DestinationField == e.Key);
                        if (mappingType != null)
                        {
                            fieldType = structure[mappingType.SourceField];
                        }
                        else
                        {
                            fieldType = structure[e.Key];
                        }
                        tEntity.Properties.Add(e.Key, GetEntityProperty(e.Key, e.Value.ToString(), fieldType));
                    }
                }

                TableOperation replaceOperation = TableOperation.Replace(tEntity);
                var response = await _table.ExecuteAsync(replaceOperation);

                if (response.HttpStatusCode != 204)
                {
                    throw new DbOperationException(response.HttpStatusCode.ToString(), JsonConvert.SerializeObject(response.Result));
                }
            }
            catch (Exception e)
            {
                throw new DbOperationException("Error-0002", e.Message);
            }
        }
        public async Task Delete(JObject entity)
        {
            try
            {
                TableEntity tEntity = new TableEntity(entity["PartitionKey"].ToString(), entity["RowKey"].ToString())
                {
                    ETag = entity["ETag"].ToString()
                };

                TableOperation deleteOperation = TableOperation.Delete(tEntity);
                var response = await _table.ExecuteAsync(deleteOperation);

                if (response.HttpStatusCode != 204)
                {
                    throw new DbOperationException(response.HttpStatusCode.ToString(), JsonConvert.SerializeObject(response.Result));
                }
            }
            catch (Exception e)
            {
                throw new DbOperationException("Error-0002", e.Message);
            }

        }

        public async Task Insert(JObject entity, List<DataFieldsMapping> fieldMappings = null)
        {
            try
            {
                var structure = GetTableStructure();

                if (!structure.ContainsKey("id"))
                {
                    entity.Remove("id"); // this is not used if is not part of the table schema
                }

                ElasticTableEntity tEntity = new ElasticTableEntity();

                var id = Guid.NewGuid().ToString();

                tEntity.PartitionKey = entity["PartitionKey"] != null ? entity["PartitionKey"].ToString() : id;
                tEntity.RowKey = entity["RowKey"] != null ? entity["RowKey"].ToString() : id;
                tEntity.ETag = $"W/\"datetime'{DateTime.Now:yyyy-MM-ddTHH%3Amm%3Ass.fffZ}'\""; ;

                foreach (var e in entity)
                {
                    if (e.Key != "PartitionKey" && e.Key != "RowKey" && e.Key != "ETag")
                    {
                        EdmType fieldType;
                        if (structure.Any())
                        {

                            var mappingType = fieldMappings?.Where(w => w.MappingType != MappingType.TableJoin).FirstOrDefault(a => a.DestinationField == e.Key);
                            if (mappingType != null)
                            {
                                fieldType = structure[mappingType.DestinationField];
                            }
                            else
                            {
                                fieldType = structure[e.Key];
                            }
                        }
                        else
                        {
                            switch (e.Value.GetType().Name)
                            {
                                case "Int32":
                                    fieldType = EdmType.Int32;
                                    break;
                                case "Int64":
                                    fieldType = EdmType.Int64;
                                    break;
                                case "Boolean":
                                    fieldType = EdmType.Boolean;
                                    break;
                                case "Double":
                                    fieldType = EdmType.Double;
                                    break;
                                case "DateTime":
                                    fieldType = EdmType.Double;
                                    break;
                                case "Guid":
                                    fieldType = EdmType.Guid;
                                    break;
                                case "Byte":
                                    fieldType = EdmType.Binary;
                                    break;
                                default:
                                    fieldType = EdmType.DateTime;
                                    break;
                            }

                            tEntity.Properties.Add(e.Key, GetEntityProperty(e.Key, e.Value.ToString(), EdmType.String));
                        }

                        tEntity.Properties.Add(e.Key, GetEntityProperty(e.Key, e.Value.ToString(), fieldType));
                    }
                }

                foreach (var s in structure.Where(s => entity.SelectToken(s.Key) == null))
                {
                    tEntity.Properties.Add(s.Key, GetEntityProperty(s.Key, null, structure[s.Key]));
                }

                TableOperation replaceOperation = TableOperation.InsertOrMerge(tEntity);
                var response = await _table.ExecuteAsync(replaceOperation);

                if (response.HttpStatusCode != 204)
                {
                    throw new DbOperationException(response.HttpStatusCode.ToString(), JsonConvert.SerializeObject(response.Result));
                }
            }
            catch (Exception e)
            {
                throw new DbOperationException("Error-0001", e.Message);
            }
        }

        public async Task CreateTable()
        {
            await _table.CreateIfNotExistsAsync();
        }


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

        private Dictionary<string, EdmType> GetTableStructure()
        {
            Dictionary<string, EdmType> properties = new();
            TableQuery<DynamicTableEntity> projectionQuery = new TableQuery<DynamicTableEntity>().Take(1);

            TableQuerySegment<DynamicTableEntity> queryResult =
                _table.ExecuteQuerySegmentedAsync(projectionQuery, null).GetAwaiter().GetResult();

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



        private static string CreateConnectionString(DataSettings settings)
        {
            return $"{nameof(DefaultEndpointsProtocol)}={DefaultEndpointsProtocol};" +
                   $"AccountName={settings.GetAccountName()};" +
                   $"AccountKey={settings.GetAuthKey()};" +
                   "EndpointSuffix=core.windows.net;";
        }

        private EntityProperty GetEntityProperty(string key, string value, EdmType type)
        {
            if (type == EdmType.Binary) return new EntityProperty(!string.IsNullOrEmpty(value) ? Encoding.ASCII.GetBytes(value) : new byte[] { });
            if (type == EdmType.Boolean) return new EntityProperty(!string.IsNullOrEmpty(value) ? bool.Parse(value) : null);
            if (type == EdmType.DateTime) return new EntityProperty(!string.IsNullOrEmpty(value) ? DateTimeOffset.Parse(value) : null);
            if (type == EdmType.Double) return new EntityProperty(!string.IsNullOrEmpty(value) ? double.Parse(value) : null);
            if (type == EdmType.Guid) return new EntityProperty(!string.IsNullOrEmpty(value) ? Guid.Parse(value) : null);
            if (type == EdmType.Int32) return new EntityProperty(!string.IsNullOrEmpty(value) ? int.Parse(value) : null);
            if (type == EdmType.Int64) return new EntityProperty(!string.IsNullOrEmpty(value) ? long.Parse(value) : null);
            if (type == EdmType.String) return new EntityProperty(value);
            throw new Exception("not supported " + value.GetType() + " for " + key);
        }
    }



    public class ElasticTableEntity : DynamicObject, ITableEntity
    {
        public ElasticTableEntity()
        {
            this.Properties = new Dictionary<string, EntityProperty>();
        }

        public IDictionary<string, EntityProperty> Properties { get; private set; }

        public object this[string key]
        {
            get
            {
                if (!this.Properties.ContainsKey(key))
                    this.Properties.Add(key, this.GetEntityProperty(key, null));

                return this.Properties[key];
            }
            set
            {
                var property = this.GetEntityProperty(key, value);

                if (this.Properties.ContainsKey(key))
                    this.Properties[key] = property;
                else
                    this.Properties.Add(key, property);
            }
        }

        #region DynamicObject overrides

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            result = this[binder.Name];
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            this[binder.Name] = value;
            return true;
        }

        #endregion

        #region ITableEntity implementation

        public string PartitionKey { get; set; }

        public string RowKey { get; set; }

        public DateTimeOffset Timestamp { get; set; }

        public string ETag { get; set; }

        public void ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            this.Properties = properties;
        }

        public IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
        {
            return this.Properties;
        }

        #endregion

        #region ICustomMemberProvider implementation for LinqPad's Dump

        public IEnumerable<string> GetNames()
        {
            return new[] { "PartitionKey", "RowKey", "Timestamp", "ETag" }
                .Union(this.Properties.Keys);
        }

        public IEnumerable<Type> GetTypes()
        {
            return new[] { typeof(string), typeof(string), typeof(DateTimeOffset), typeof(string) }
                .Union(this.Properties.Values.Select(x => this.GetType(x.PropertyType)));
        }

        public IEnumerable<object> GetValues()
        {
            return new object[] { this.PartitionKey, this.RowKey, this.Timestamp, this.ETag }
                .Union(this.Properties.Values.Select(x => this.GetValue(x)));
        }

        #endregion

        private EntityProperty GetEntityProperty(string key, object value)
        {
            if (value == null) return new EntityProperty((string)null);
            if (value.GetType() == typeof(byte[])) return new EntityProperty((byte[])value);
            if (value.GetType() == typeof(bool)) return new EntityProperty((bool)value);
            if (value.GetType() == typeof(DateTimeOffset)) return new EntityProperty((DateTimeOffset)value);
            if (value.GetType() == typeof(DateTime)) return new EntityProperty((DateTime)value);
            if (value.GetType() == typeof(double)) return new EntityProperty((double)value);
            if (value.GetType() == typeof(Guid)) return new EntityProperty((Guid)value);
            if (value.GetType() == typeof(int)) return new EntityProperty((int)value);
            if (value.GetType() == typeof(long)) return new EntityProperty((long)value);
            if (value.GetType() == typeof(string)) return new EntityProperty((string)value);
            throw new Exception("not supported " + value.GetType() + " for " + key);
        }

        private Type GetType(EdmType edmType)
        {
            switch (edmType)
            {
                case EdmType.Binary: return typeof(byte[]);
                case EdmType.Boolean: return typeof(bool);
                case EdmType.DateTime: return typeof(DateTime);
                case EdmType.Double: return typeof(double);
                case EdmType.Guid: return typeof(Guid);
                case EdmType.Int32: return typeof(int);
                case EdmType.Int64: return typeof(long);
                case EdmType.String: return typeof(string);
                default: throw new Exception("not supported " + edmType);
            }
        }

        private object GetValue(EntityProperty property)
        {
            switch (property.PropertyType)
            {
                case EdmType.Binary: return property.BinaryValue;
                case EdmType.Boolean: return property.BooleanValue;
                case EdmType.DateTime: return property.DateTimeOffsetValue;
                case EdmType.Double: return property.DoubleValue;
                case EdmType.Guid: return property.GuidValue;
                case EdmType.Int32: return property.Int32Value;
                case EdmType.Int64: return property.Int64Value;
                case EdmType.String: return property.StringValue;
                default: throw new Exception("not supported " + property.PropertyType);
            }
        }
    }
}