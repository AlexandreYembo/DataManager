using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage;
using System.Dynamic;
using System.Text;

namespace Connectors.Azure.TableStorage
{
    /// <summary>
    /// Class that Parses the dynamic object into Table Storage standards
    /// </summary>
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

        public object this[string key, EdmType type]
        {
            get
            {
                if (!this.Properties.ContainsKey(key))
                    this.Properties.Add(key, this.GetEntityProperty(key, null, type));

                return this.Properties[key];
            }
            set
            {
                var property = this.GetEntityProperty(key, value, type);

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

        private EntityProperty GetEntityProperty(string key, object value, EdmType type)
        {
            if (value == null) return new EntityProperty((string)null);
            if (type == EdmType.Binary) return new EntityProperty((byte[])value);
            if (type == EdmType.Boolean) return new EntityProperty((bool)value);
            if (type == EdmType.DateTime) return new EntityProperty((DateTime)value);
            if (type == EdmType.Double) return new EntityProperty((double)value);
            if (type == EdmType.Guid) return new EntityProperty((Guid)value);
            if (type == EdmType.Int32) return new EntityProperty((int)value);
            if (type == EdmType.Int64) return new EntityProperty((long)value);
            if (type == EdmType.String) return new EntityProperty((string)value);
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
