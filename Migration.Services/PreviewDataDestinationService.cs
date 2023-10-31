using Migration.Repository;
using Migration.Repository.DbOperations;
using Migration.Repository.Helpers;
using Migration.Repository.Models;
using Migration.Services.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Services
{
    //public class PreviewDataDestinationService : IPreviewDataService
    //{
    //    private readonly IGenericRepository _genericRepository;
    //    private readonly DBSettings _settings;

    //    public PreviewDataDestinationService(DBSettings settings, Func<DBSettings, IGenericRepository> genericRepository)
    //    {
    //        _genericRepository = genericRepository(settings);
    //    }

    //    public Dictionary<string, List<JObject>> Preview(List<DataFieldsMapping> dataFieldsMappings, List<(DataType dataType, List<DynamicData> record)> data)
    //    {
    //        //var commands = listCommands.Select(s => MapFieldTypes.BuildCommandDictionary(s));

    //        var result = new Dictionary<string, List<JObject>>();

    //        var source = data.Where(w => w.dataType == DataType.Source).SelectMany(s => s.record).ToList();
    //        var destinations = data.Where(w => w.dataType == DataType.Destination).SelectMany(s => s.record).ToList();

    //        //result = source.Join(destinations,
    //        //    s => s.Id,
    //        //    d => dataFieldsMappings.Where(w => w.DataFieldType == DataFieldType.tableJoin)
    //        //        .SelectMany(s => JObject.Parse(d.Data)[s.SourceField]),
    //        //    (s, d) => new JObject
    //        //    {

    //        //    });



    //        foreach (var entity in destinations.Select(s => s))
    //        {
    //            var joins = dataFieldsMappings.Where(w => w.DataFieldType == DataFieldType.tableJoin);

    //            //Create 2 objects, one to persist original data, and one that will receive the changes
    //            JObject objectToBeUpdated = JObject.Parse(entity.Data);
    //            JObject originalData = JObject.Parse(entity.Data);

    //            var id = ((JValue)objectToBeUpdated["id"]);

    //            bool hasChange = false;

    //            foreach (var mapping in dataFieldsMappings.Where(w => w.DataFieldType == DataFieldType.fieldMapping).Select(s => s))
    //            {
    //                var fieldsArr = mapping.Key.Split(".").ToList();

    //                //Apply the change to the current property
    //                objectToBeUpdated = JObjectHelper.GetObject(objectToBeUpdated, fieldsArr, command.Value);

    //                hasChange = true;
    //            }

    //            if (!hasChange) continue;

    //            result.Add(id.ToString(), new List<JObject>() { originalData, objectToBeUpdated });
    //        }

    //        return result;
    //    }
    //}
}