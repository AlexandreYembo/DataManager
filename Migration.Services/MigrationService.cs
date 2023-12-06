using Migration.Repository;
using Migration.Repository.Extensions;
using Migration.Repository.Helpers;
using Migration.Repository.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Services
{
    public interface IMigrationService
    {
        Task Migrate(DataMapping dataMapping);
    }

    public class MigrationService : IMigrationService
    {
        private readonly Func<DataSettings, IGenericRepository> _genericRepository;

        public MigrationService(Func<DataSettings, IGenericRepository> genericRepository)
        {
            _genericRepository = genericRepository;
        }

        public async Task Migrate(DataMapping dataMapping)
        {
            List<Task> processTaks = new();

            ////TODO: Obtain records already processed (can use pagination)
            //var source = await _genericRepository(dataMapping.Source.Settings)
            //    .Get(dataMapping.Source.Query);

            //if (!source.Any())
            //    return;

            //var mappingMergeFields = dataMapping.FieldsMapping
            //    .Where(w => w.MappingType == MappingType.FieldValueMerge).ToList();

            //foreach (var sourceData in source)
            //{
            //    processTaks.Add(ProcessDestinationRecordsAsync(dataMapping, sourceData, source, mappingMergeFields));
            //}

            //TODO: Subscribe an event to add the records already processed, can save pagination OFFSET <offset_amount> LIMIT <limit_amount>
            //https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query/offset-limit

            await Task.WhenAll(processTaks);
        }

        //private async Task ProcessDestinationRecordsAsync(DataMapping dataMapping, KeyValuePair<string, string> sourceData, Dictionary<string, string> source, List<DataFieldsMapping> mappingMergeFields)
        //{
        //    var destination = await _genericRepository(dataMapping.Destination.Settings)
        //        .Get(dataMapping.Destination.Query, dataMapping.FieldsMapping, sourceData.Value, 15);

        //    var listDestination = destination.ApplyJoin(source, dataMapping.FieldsMapping);

        //    if (!listDestination.Any()) return;

        //    var sourceObj = JObject.Parse(sourceData.Value);

        //    foreach (var d in listDestination)
        //    {
        //        bool hasChange = false;

        //        var originalData = d;
        //        var objectToBeUpdated = d;

        //        foreach (var mappingMergeField in mappingMergeFields)
        //        {
        //            var fieldsFromSourceArr = mappingMergeField.SourceField.Split(".").ToList();

        //            var valueFromSource = JObjectHelper.GetValueFromObject(sourceObj, fieldsFromSourceArr);

        //            var fieldsFromDestinationArr = mappingMergeField.DestinationField.Split(".").ToList();

        //            objectToBeUpdated = JObjectHelper.GetObject(objectToBeUpdated, fieldsFromDestinationArr,
        //                valueFromSource);
        //            hasChange = true;
        //        }

        //        if (!hasChange) return;
        //    }

        //}
    }
}