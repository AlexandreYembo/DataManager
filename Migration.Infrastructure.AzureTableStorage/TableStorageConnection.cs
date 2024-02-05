using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Migration.Repository;

namespace Migration.Infrastructure.AzureTableStorage
{
    public class TableStorageConnection : ITestConnection
    {
        protected const string DefaultEndpointsProtocol = "https";

        private readonly CloudStorageAccount _storageAccount;
        private readonly CloudTableClient _tableClient;
        private readonly DataSettings _settings;

        public TableStorageConnection(DataSettings settings)
        {
            _settings = settings;
            _storageAccount = settings.Parameters.Any(p => p.Key == "Is Emulator" && p.Value == "True") ?
                                                    CloudStorageAccount.DevelopmentStorageAccount :
                                                    CloudStorageAccount.Parse(CreateConnectionString(settings));

            _tableClient = _storageAccount.CreateCloudTableClient();
        }
        public Task<DataSettings> Test()
        {
            _settings.Entities = new();

            var listTables = _tableClient.ListTablesSegmentedAsync(null).GetAwaiter().GetResult();

            foreach (var table in listTables)
            {
                _settings.Entities.Add(new (table.Name)
                {
                    Attributes = new()
                    {
                        new()
                        {
                            Key = "PartitionKey"
                        },
                        new()
                        {
                            Key = "RowKey"
                        }
                    }
                });
            }

            return Task.FromResult(_settings);
        }

        private static string CreateConnectionString(DataSettings settings)
        {
            return $"{nameof(DefaultEndpointsProtocol)}={DefaultEndpointsProtocol};" +
                   $"AccountName={settings.GetAccountName()};" +
                   $"AccountKey={settings.GetAuthKey()};" +
                   "EndpointSuffix=core.windows.net;";
        }
    }
}
