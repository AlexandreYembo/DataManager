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

            try
            {
                var source = await _genericRepository(dataMapping.Source.Settings)
                    .Get(dataMapping.Source.Query);

                if (!source.Any())
                    return new();

                foreach (var sourceData in source)
                {
                    var destination = await _genericRepository(dataMapping.Destination.Settings)
                 .Get(dataMapping.Destination.Query, dataMapping.FieldsMapping, sourceData.Value, take);

                    if (destination.Any())
                    {
                        //To avoid add duplicated record
                        if (!result.Values.Any(a => a.Any(a1 => destination.ContainsValue(a1.Data))))

                        result.Add($"{dataMapping.Destination.Settings.CurrentEntity}:{sourceData.Key}", 
                            destination.ApplyJoin(source, dataMapping.FieldsMapping).ToDynamicDataList(JObject.Parse(sourceData.Value)));
                    }
                    else
                    {
                        result.Add(sourceData.Key, sourceData.ToDynamicDataList(DataType.Source));
                    }
                }

                return result;
            }
            catch
            {
                return result;
            }
        }
    }
}