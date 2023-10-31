using Migration.Repository;
using Migration.Repository.Extensions;
using Migration.Repository.Models;
using Migration.Services.Extensions;
using Migration.Services.Models;

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
            try
            {
                var source = await _genericRepository(dataMapping.Source.Settings)
                    .Get(dataMapping.Source.Query);

                if (!source.Any())
                    return new();

                var destination = await _genericRepository(dataMapping.Destination.Settings)
                    .Get(dataMapping.Destination.Query, dataMapping.FieldsMapping, source.Select(s => s.Value), take);

                Dictionary<string, List<DynamicData>> result = new()
                {
                    {dataMapping.Source.Settings.CurrentEntity, source.ToDynamicDataList()},
                    {dataMapping.Destination.Settings.CurrentEntity, destination.ApplyJoin(source, dataMapping.FieldsMapping).ToDynamicDataList()},
                };

                return result;
            }
            catch
            {
                return new();
            }
        }
    }
}