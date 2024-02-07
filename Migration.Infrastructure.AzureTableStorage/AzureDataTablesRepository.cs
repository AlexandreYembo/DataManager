
using Azure.Data.Tables;
using Migration.Repository;
using Migration.Repository.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Infrastructure.AzureTableStorage
{
    /// <summary>
    /// TODO: New class to suppor the new Library -> underdevelopment
    /// </summary>
    public class AzureDataTablesRepository : IGenericRepository
    {
        protected const string DefaultEndpointsProtocol = "https";
        private readonly TableServiceClient _tableServiceClient;
        private readonly TableClient _tableClient;

        public AzureDataTablesRepository(DataSettings settings)
        {
            _tableServiceClient = new(CreateConnectionString(settings));
            _tableClient = _tableServiceClient.GetTableClient(settings.CurrentEntity.Name);

        }

        private static string CreateConnectionString(DataSettings settings)
        {
            return $"{nameof(DefaultEndpointsProtocol)}={DefaultEndpointsProtocol};" +
                   $"AccountName={settings.GetAccountName()};" +
                   $"AccountKey={settings.GetAuthKey()};" +
                   "EndpointSuffix=core.windows.net;";
        }

        public async Task<Dictionary<string, string>> Get(string query)
        {
            var entities = await _tableClient.GetAllEntitiesStartingWithAsync<TableEntity>("CustomAttributes", "ConModMQBw_RU");

            foreach (var e in entities)
            {
            }

            return new Dictionary<string, string>();
        }

        public Task<Dictionary<string, string>> Get(string rawQuery, List<DataFieldsMapping> fieldMappings, string data, int take, int skip = 0)
        {
            throw new NotImplementedException();
        }

        public Task<Dictionary<string, string>> GetTop5(string rawQuery)
        {
            throw new NotImplementedException();
        }

        public Task<Dictionary<string, string>> GetByListIds(string[] ids)
        {
            throw new NotImplementedException();
        }

        public Task Update(JObject entity, List<DataFieldsMapping> fieldMappings = null)
        {
            throw new NotImplementedException();
        }

        public Task Delete(JObject entity)
        {
            throw new NotImplementedException();
        }

        public Task Insert(JObject entity, List<DataFieldsMapping> fieldMappings = null)
        {
            throw new NotImplementedException();
        }

        public Task CreateTable()
        {
            throw new NotImplementedException();
        }
    }
}
