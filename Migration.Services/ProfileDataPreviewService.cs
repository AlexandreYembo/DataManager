using Migration.Core;
using Migration.Models;
using Migration.Models.Profile;
using Migration.Services.Extensions;
using Migration.Services.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Services
{
    public interface IQueryService
    {
        //key is the primary key of the source table
        //values contains the list of Source and Destination tables when it applies
        Task<Dictionary<string, List<DynamicData>>> Get(ProfileConfiguration profile, int take);
    }

    public class ProfileDataPreviewService : IQueryService
    {
        private readonly Func<DataSettings, IGenericRepository> _genericRepository;

        public ProfileDataPreviewService(Func<DataSettings, IGenericRepository> genericRepository)
        {
            _genericRepository = genericRepository;
        }

        public async Task<Dictionary<string, List<DynamicData>>> Get(ProfileConfiguration profile, int take)
        {
            Dictionary<string, List<DynamicData>> result = new();

            var source = await _genericRepository(profile.Source.Settings)
                .GetAsync(new RepositoryParameters()
                {
                    Query = profile.Source.Query,
                    FieldMappings = profile.FieldsMapping,
                    Pagination = new()
                    {
                        Take = 10,
                        Skip = 0
                    }
                });

            if (!source.Any())
                return new();

            Dictionary<string, JObject> dataSource = new();

            foreach (var sourceData in source) //TODO: need to group source by the join applied to avoid making multiple queries for the same relationship
            {
                var jsonObject = sourceData.Value;

                if (jsonObject["id"] != null)
                {
                    dataSource.Add($"{profile.Source.Settings.CurrentEntity.Name}:{jsonObject["id"]}", jsonObject);
                }
                else
                {
                    dataSource.Add($"{profile.Source.Settings.CurrentEntity.Name}:{Guid.NewGuid()}", jsonObject);
                }

                if (profile.DataQueryMappingType == DataQueryMappingType.SourceToTarget && profile.OperationType != OperationType.Import)
                {
                    var destination = await _genericRepository(profile.Target.Settings)
                        .GetAsync(new RepositoryParameters()
                        {
                            Query = profile.Target.Query,
                            FieldMappings = profile.FieldsMapping,
                            Pagination = new()
                            {
                                Take = take,
                                Skip = 0
                            }
                        });

                    Dictionary<string, IEnumerable<JObject>> dataDestination = new();
                    if (destination.Any())
                    {
                        //To avoid add duplicated record
                        if (!result.Values.Any(a => a.Any(a1 => destination.ContainsValue(a1.Data))))
                        {
                            dataDestination.Add(profile.Target.Settings.CurrentEntity.Name, destination.ApplyJoin(sourceData, profile.FieldsMapping));
                            result.Add($"{profile.Source.Settings.CurrentEntity.Name}:{sourceData.Key}", dataDestination.ToDynamicDataList(sourceData, profile.Source.Settings.CurrentEntity.Name, profile.OperationType));
                        }
                    }
                }
                else
                {
                    result.Add($"{profile.Source.Settings.CurrentEntity.Name}:{sourceData.Key}", dataSource.LastOrDefault().ToDynamicDataList());
                }
            }

            return result;
        }
    }
}