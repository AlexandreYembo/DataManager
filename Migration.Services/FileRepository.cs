using Migration.Core;
using Migration.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Services
{
    public class FileRepository : IGenericRepository
    {
        private readonly string _fileName;

        public FileRepository(DataSettings settings)
        {
            _fileName = settings.GetFileName();
        }

        //public async Task<Dictionary<string, string>> Get(string query)
        //{
        //    var filterExpression = CreateFilterExpression(query);

        //    var text = await File.ReadAllTextAsync(_fileName);

        //    Dictionary<string, string> dictionary = new();

        //    if (text.StartsWith("[") && text.EndsWith("]")) // Is valid array
        //    {
        //        var array = JArray.Parse(text);
        //        foreach (var jToken in array.AsQueryable().AsEnumerable().Where(CreateFilterFunction(filterExpression)))
        //        {
        //            var value = ((JValue)jToken["id"]).Value;
        //            dictionary[value.ToString()] = jToken.ToString(); ;
        //        }
        //    }
        //    else if (text.StartsWith("{") && text.EndsWith("}")) // Is valid object
        //    {
        //        var jObject = JObject.Parse(text);

        //        var value = ((JValue)jObject["id"]).Value;
        //        dictionary[value.ToString()] = jObject.ToString();
        //    }

        //    return dictionary;
        //}

        //private static Func<JToken, bool> CreateFilterFunction(string filterCondition)
        //{
        //    // You would typically want to add validation and error handling here.
        //    // For simplicity, this example assumes that the filter condition is valid.

        //    // Create a parameter expression for the JToken input
        //    ParameterExpression parameter = Expression.Parameter(typeof(JToken), "item");

        //    // Create a lambda expression for the filter condition
        //    Expression filterExpression = DynamicExpressionParser.ParseLambda(
        //        new[] { parameter }, typeof(bool), filterCondition);

        //    // Compile the lambda expression into a Func<JToken, bool>
        //    return Expression.Lambda<Func<JToken, bool>>(filterExpression, parameter).Compile();
        //}

        //private string CreateFilterExpression(string rawQuery)
        //{
        //    var where = rawQuery.Substring(rawQuery.IndexOf("where") + 5);
        //    string filterExpression = where.Replace("and", "&&");

        //    return filterExpression;
        //}

        public Task<DataSettings> TestConnection()
        {
            throw new NotImplementedException();
        }

        public Task<Dictionary<string, JObject>> GetAsync(string query)
        {
            throw new NotImplementedException();
        }

        public Task<Dictionary<string, JObject>> GetAsync(RepositoryParameters parameters)
        {
            throw new NotImplementedException();
        }

        public Task UpdateAsync(RepositoryParameters parameters)
        {
            throw new NotImplementedException();
        }

        public Task DeleteAsync(RepositoryParameters parameters)
        {
            throw new NotImplementedException();
        }

        public Task InsertAsync(RepositoryParameters parameters)
        {
            throw new NotImplementedException();
        }

        public Task CreateTableAsync()
        {
            throw new NotImplementedException();
        }
    }
}
