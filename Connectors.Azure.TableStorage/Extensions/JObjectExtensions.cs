using Microsoft.WindowsAzure.Storage.Table;
using Migration.Core;
using Migration.Models;
using Migration.Models.Profile;
using Newtonsoft.Json.Linq;

namespace Connectors.Azure.TableStorage.Extensions
{
    public static class JObjectExtensions
    {
        public static ElasticTableEntity ConvertToElasticTableEntity(this JObject entity, Dictionary<string, EdmType> tableStructure, List<DataFieldsMapping> fieldMappings)
        {
            ElasticTableEntity tEntity = new ElasticTableEntity();

            var id = Guid.NewGuid().ToString();

            tEntity.PartitionKey = entity["PartitionKey"] != null ? entity["PartitionKey"].ToString() : id;
            tEntity.RowKey = entity["RowKey"] != null ? entity["RowKey"].ToString() : id;

            foreach (var e in entity)
            {
                if (e.Key != Constants.PARTITION_KEY && e.Key != Constants.ROW_KEY && e.Key != Constants.E_TAG)
                {
                    EdmType fieldType;
                    if (tableStructure.Any())
                    {
                        if (!tableStructure.ContainsKey("id"))
                        {
                            entity.Remove("id"); // this is not used if is not part of the table schema
                        }

                        var mappingType = fieldMappings?.Where(w => w.MappingType != MappingType.TableJoin).FirstOrDefault(a => a.TargetField == e.Key);
                        if (mappingType != null)
                        {
                            fieldType = tableStructure[mappingType.TargetField];
                        }
                        else
                        {
                            fieldType = tableStructure[e.Key];
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
                                fieldType = EdmType.DateTime;
                                break;
                            case "Guid":
                                fieldType = EdmType.Guid;
                                break;
                            case "Byte":
                                fieldType = EdmType.Binary;
                                break;
                            default:
                                fieldType = EdmType.String;
                                break;
                        }
                    }

                    tEntity[e.Key, fieldType] = e.Value;
                }
            }

            //Fill property null keeping the type if the incoming values are null
            foreach (var s in tableStructure.Where(s => entity.SelectToken(s.Key) == null))
            {
                tEntity[s.Key, tableStructure[s.Key]] = null;
            }

            return tEntity;
        }
    }
}
