using Migration.Repository;
using Migration.Repository.Extensions;
using Migration.Repository.Models;
using Migration.Services.Extensions;
using Migration.Services.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Services
{
    public interface IQueryService
    {
        //key is the primary key of the source table
        //values contains the list of Source and Destination tables when it applies
        Task<List<DynamicData>> Get(DataMapping dataMapping, int take);
    }

    public class MutipleQueriesService : IQueryService
    {
        private readonly Func<DataSettings, IGenericRepository> _genericRepository;

        public MutipleQueriesService(Func<DataSettings, IGenericRepository> genericRepository)
        {
            _genericRepository = genericRepository;
        }

        public async Task<List<DynamicData>> Get(DataMapping dataMapping, int take)
        {
            Dictionary<string, string> listSourceData = new();
            Dictionary<string, string> listDestinationData = new();

            foreach (var sourceConfig in dataMapping.Source)
            {
                var sourceFieldMappings = dataMapping.FieldsMapping.Where(w =>
                    w.DestinationEntity == sourceConfig.Settings.CurrentEntity
                    || w.SourceEntity == sourceConfig.Settings.CurrentEntity
                    && w.JoinType == JoinType.BetweenSource).ToList();

                if (listSourceData.Count == 0)
                {
                    var source = await _genericRepository(sourceConfig.Settings).Get(sourceConfig.Query);

                    if (!source.Any()) continue;

                    listSourceData = ConcatMethod(listSourceData, source);
                }
                else if (sourceFieldMappings.Any() &&
                         (listSourceData.Any(w => w.Key.Split(":").FirstOrDefault() == sourceFieldMappings.FirstOrDefault().SourceEntity)
                          || listSourceData.Any(w => w.Key.Split(":").FirstOrDefault() == sourceFieldMappings.FirstOrDefault().DestinationEntity)))
                {
                    var repository = _genericRepository(sourceConfig.Settings);

                    foreach (var sourceData in listSourceData.Where(w => w.Key.Split(":").FirstOrDefault() == sourceConfig.Settings.CurrentEntity))
                    {
                        var source = await repository.Get(sourceConfig.Query, sourceFieldMappings, sourceData.Value, take);

                        if (!source.Any()) continue;

                        listSourceData = ConcatMethod(listSourceData, source);
                    }
                }
            }

            foreach (var destinationConfig in dataMapping.Destination)
            {
                var destinationFieldMappings = dataMapping.FieldsMapping.Where(w =>
                    w.DestinationEntity == destinationConfig.Settings.CurrentEntity
                    && w.JoinType == JoinType.Destination).ToList();

                if (destinationFieldMappings.Count == 0) continue;

                var destination = await _genericRepository(destinationConfig.Settings)
                    .Get(destinationConfig.Query, destinationFieldMappings, listSourceData, take);

                listDestinationData = ConcatMethod(listDestinationData, destination);
            }

            var result = listSourceData.ToDynamicDataList(listDestinationData);

            return result;
        }

        private Dictionary<TKey, TValue> ConcatMethod<TKey, TValue>(params Dictionary<TKey, TValue>[] dictionaries)
        {
            var mergedDictionary = dictionaries.Aggregate((dict1, dict2) =>
                dict1.Concat(dict2).ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
            return mergedDictionary;
        }
    }
}