using Migration.Repository.Models;
using Migration.Services.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Services
{
    public interface IPreviewDataService
    {
        Dictionary<string, List<JObject>> Preview(List<DataFieldsMapping> dataFieldsMappings, List<DynamicData> source, List<DynamicData> destination);
    }
}