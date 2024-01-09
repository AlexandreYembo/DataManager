//using Microsoft.Azure.Cosmos.Table;

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Migration.Repository;
using Migration.Repository.Exceptions;
using Migration.Repository.Models;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Dynamic;
using System.Text;

namespace Migration.Infrastructure.AzureTableStorage
{
    public class TableStorageGenericRepository : IGenericRepository
    {
        protected const string DefaultEndpointsProtocol = "https";

        private readonly CloudStorageAccount _storageAccount;
        private readonly CloudTableClient _tableClient;
        private readonly CloudTable _table;

        public TableStorageGenericRepository(DataSettings settings)
        {
            if (!string.IsNullOrEmpty(settings.CurrentEntity))
            {
                _storageAccount = settings.Parameters.Any(p => p.Key == "Is Emulator" && p.Value == "True") ?
                    CloudStorageAccount.DevelopmentStorageAccount :
                    CloudStorageAccount.Parse(CreateConnectionString(settings));

                _tableClient = _storageAccount.CreateCloudTableClient();
                _table = _tableClient.GetTableReference(settings.CurrentEntity);
            }
        }

        public async Task<Dictionary<string, string>> Get(string rawQuery)
        {
            Dictionary<string, string> dictionary = new();

            var query = QueryBuilder.Build(rawQuery, _table);

            var tableQuery = new TableQuery<DynamicTableEntity>().Where(query);

            var segment = await _table.ExecuteQuerySegmentedAsync(tableQuery, null);

            foreach (DynamicTableEntity entity in segment.Results)
            {
                // Access properties dynamically
                JObject jo = new JObject();

                string partitionKey = entity.PartitionKey;
                string rowKey = entity.RowKey;

                jo.Add("id", $"{partitionKey}:{rowKey}");
                jo.Add("PartitionKey", partitionKey);
                jo.Add("RowKey", partitionKey);
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
            Dictionary<string, string> dictionary = new();
            var query = QueryBuilder.Build(rawQuery, _table, fieldMappings, data, take);

            var tableQuery = new TableQuery<DynamicTableEntity>().Where(query);

            var segment = await _table.ExecuteQuerySegmentedAsync(tableQuery, null);

            foreach (DynamicTableEntity entity in segment.Results.Skip(skip).Take(take)) // it needs to be changed, performance issue
            {
                // Access properties dynamically
                JObject jo = new JObject();

                string partitionKey = entity.PartitionKey;
                string rowKey = entity.RowKey;

                jo.Add("id", $"{partitionKey}:{rowKey}");

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
                throw new DbOperationException("Error-0001", e.Message);
            }
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

        private static string CreateConnectionString(DataSettings settings)
        {
            return $"{nameof(DefaultEndpointsProtocol)}={DefaultEndpointsProtocol};" +
                   $"AccountName={settings.GetAccountName()};" +
                   $"AccountKey={settings.GetAuthKey()};" +
                   "EndpointSuffix=core.windows.net;";
        }

        private EntityProperty GetEntityProperty(string key, string value, EdmType type)
        {
            if (type == EdmType.Binary) return new EntityProperty(Encoding.ASCII.GetBytes(value));
            if (type == EdmType.Boolean) return new EntityProperty(bool.Parse(value));
            if (type == EdmType.DateTime) return new EntityProperty(DateTimeOffset.Parse(value));
            if (type == EdmType.Double) return new EntityProperty(double.Parse(value));
            if (type == EdmType.Guid) return new EntityProperty(Guid.Parse(value));
            if (type == EdmType.Int32) return new EntityProperty(int.Parse(value));
            if (type == EdmType.Int64) return new EntityProperty(long.Parse(value));
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