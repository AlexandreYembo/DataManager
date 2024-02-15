using Migration.Models.Profile;
using Newtonsoft.Json.Linq;

namespace Migration.Models
{
    public class RepositoryParameters
    {
        public string Query { get; set; }

        public List<DataFieldsMapping> FieldMappings { get; set; }
        public JObject Data { get; set; }
        public Pagination Pagination { get; set; }
    }

    public class Pagination
    {
        public int Take { get; set; }
        public int Skip { get; set; }
    }
}