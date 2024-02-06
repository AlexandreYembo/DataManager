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
        Task<Dictionary<string, List<DynamicData>>> Get(DataMapping dataMapping, int take);
    }

    public class MutipleQueriesService : IQueryService
    {
        private readonly Func<DataSettings, IGenericRepository> _genericRepository;

        public MutipleQueriesService(Func<DataSettings, IGenericRepository> genericRepository)
        {
            _genericRepository = genericRepository;
        }

        public async Task<Dictionary<string, List<DynamicData>>> Get(DataMapping dataMapping, int take)
        {
            Dictionary<string, List<DynamicData>> result = new();

            var source = await _genericRepository(dataMapping.Source.Settings)
                .Get(dataMapping.Source.Query, null, null, 10, 0);

            if (!source.Any())
                return new();

            Dictionary<string, JObject> dataSource = new();

            foreach (var sourceData in source) //TODO: need to group source by the join applied to avoid making multiple queries for the same relationship
            {
                var jsonObject = JObject.Parse(sourceData.Value);

                if (jsonObject["id"] != null)
                {
                    dataSource.Add($"{dataMapping.Source.Settings.CurrentEntity.Name}:{jsonObject["id"]}", jsonObject);
                }
                else
                {
                    dataSource.Add($"{dataMapping.Source.Settings.CurrentEntity.Name}:{Guid.NewGuid()}", jsonObject);
                }

                if (dataMapping.DataQueryMappingType == DataQueryMappingType.UpdateAnotherCollection && dataMapping.OperationType != OperationType.Import)
                {
                    var destination = await _genericRepository(dataMapping.Destination.Settings)
                        .Get(dataMapping.Destination.Query, dataMapping.FieldsMapping, sourceData.Value, take, 0);

                    Dictionary<string, IEnumerable<JObject>> dataDestination = new();
                    if (destination.Any())
                    {
                        //To avoid add duplicated record
                        if (!result.Values.Any(a => a.Any(a1 => destination.ContainsValue(a1.Data))))
                        {
                            dataDestination.Add(dataMapping.Destination.Settings.CurrentEntity.Name, destination.ApplyJoin(sourceData, dataMapping.FieldsMapping));
                            result.Add($"{dataMapping.Source.Settings.CurrentEntity.Name}:{sourceData.Key}", dataDestination.ToDynamicDataList(sourceData, dataMapping.Source.Settings.CurrentEntity.Name, dataMapping.OperationType));
                        }
                    }
                }
                else
                {
                        result.Add($"{dataMapping.Source.Settings.CurrentEntity.Name}:{sourceData.Key}", dataSource.LastOrDefault().ToDynamicDataList());
                }

                //else
                //{
                //    result.Add($"{dataMapping.Source.Settings.CurrentEntity.Name}:{sourceData.Key}", dataSource.LastOrDefault().ToDynamicDataList(DataType.Source));
                //}
            }

            return result;
        }
    }
}