//using Migration.Core;
//using Migration.Models;
//using Newtonsoft.Json.Linq;
//using StackExchange.Redis;

//namespace Connectors.Redis
//{
//    public class CacheRepository : IGenericRepository
//    {
//        private readonly IDatabase _db;
//        public CacheRepository(IConnectionMultiplexer connectionMultiplexer, DataSettings settings)
//        {
//            var databaseNumber = int.Parse(settings.Parameters.FirstOrDefault(f => f.Key == "DatabaseNumber").Value);

//            _db = connectionMultiplexer.GetDatabase(databaseNumber);
//        }

//        public async Task<DataSettings> TestConnection(DataSettings settings)
//        {
//            throw new NotImplementedException();
//        }

//        public Task DeleteAsync(RepositoryParameters parameters)
//        {
//            throw new NotImplementedException();
//        }

//        public Task<Dictionary<string, JObject>> GetAsync(string query)
//        {
//            throw new NotImplementedException();
//        }

//        public Task<Dictionary<string, JObject>> GetAsync(RepositoryParameters parameters)
//        {
//            throw new NotImplementedException();
//        }

//        public async Task InsertAsync(RepositoryParameters parameters)
//        {
//            var entity = parameters.Entity;

//            await _db.SetAddAsync(entity, parameters.Data.ToString());

//    //        await _db.StringAppendAsync(entity, parameters.Data.ToString());
//        }

//        public Task<DataSettings> TestConnection()
//        {
//            throw new NotImplementedException();
//        }

//        public async Task UpdateAsync(RepositoryParameters parameters)
//        {
//            var entity = parameters.Entity;
//            await _db.StringAppendAsync(entity, parameters.Data.ToString());
//        }

//        public Task CreateTableAsync()
//        {
//            throw new NotImplementedException();
//        }
//    }
//}