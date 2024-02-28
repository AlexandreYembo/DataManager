using Connectors.Redis;
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
        private readonly ICacheRepository _cacheRepository;

        public ProfileDataPreviewService(Func<DataSettings, IGenericRepository> genericRepository,
            ICacheRepository cacheRepository)
        {
            _genericRepository = genericRepository;
            _cacheRepository = cacheRepository;
        }

        public async Task<Dictionary<string, List<DynamicData>>> Get(ProfileConfiguration profile, int take)
        {
            Dictionary<string, List<DynamicData>> result = new();

            Dictionary<string, JObject> sourceFromCache = new();
            Dictionary<string, JObject> targetFromCache = new();

            Dictionary<string, JObject> source;

            if (profile.Target.Settings.IsCacheConnection)
            {
                targetFromCache = await _cacheRepository.GetAsync(profile.Target.Settings.CurrentEntity.Name);
            }

            if (profile.Source.Settings.IsCacheConnection)
            {
                sourceFromCache = await _cacheRepository.GetAsync(profile.Source.Settings.CurrentEntity.Name);
                source = sourceFromCache.Take(10).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
            else
            {
                source = await _genericRepository(profile.Source.Settings)
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
            }

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

                    Dictionary<string, JObject> target;

                    if (profile.Target.Settings.IsCacheConnection)
                    {
                        target = _cacheRepository.GetFromCache(targetFromCache, new RepositoryParameters()
                        {
                            Query = profile.Target.Query,
                            FieldMappings = profile.FieldsMapping,
                            Data = sourceData.Value,
                            Pagination = new()
                            {
                                Take = take,
                                Skip = 0
                            }
                        });
                    }
                    else
                    {
                        target = await _genericRepository(profile.Target.Settings)
                       .GetAsync(new RepositoryParameters()
                       {
                           Query = profile.Target.Query,
                           FieldMappings = profile.FieldsMapping,
                           Data = sourceData.Value,
                           Pagination = new()
                           {
                               Take = take,
                               Skip = 0
                           }
                       });
                    }

                    Dictionary<string, IEnumerable<JObject>> dataDestination = new();
                    if (target.Any())
                    {
                        //To avoid add duplicated record
                        if (!result.Values.Any(a => a.Any(a1 => target.ContainsValue(a1.Data))))
                        {
                            dataDestination.Add(profile.Target.Settings.CurrentEntity.Name, target.ApplyJoin(sourceData, profile.FieldsMapping));
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