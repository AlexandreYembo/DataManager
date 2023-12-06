using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using Migration.Repository.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Repository
{
    public class FileRepository : IGenericRepository
    {
        private readonly string _fileName;

        private readonly DataSettings _settings;

        public FileRepository(DataSettings settings)
        {
            _fileName = settings.GetFileName();
            _settings = settings;
        }

        public async Task<Dictionary<string, string>> Get(string query)
        {
            var filterExpression = CreateFilterExpression(query);

            var text = await File.ReadAllTextAsync(_fileName);
            
            Dictionary<string, string> dictionary = new();
            
            if (text.StartsWith("[") && text.EndsWith("]")) // Is valid array
            {
                var array = JArray.Parse(text);
                foreach (var jToken in array.AsQueryable().AsEnumerable().Where(CreateFilterFunction(filterExpression)))
                {
                    var value = ((JValue)jToken["id"]).Value;
                    dictionary[$"{_settings.CurrentEntity}:{value}"] = jToken.ToString(); ;
                }
            }
            else if (text.StartsWith("{") && text.EndsWith("}")) // Is valid object
            {
                var jObject = JObject.Parse(text);

                var value = ((JValue)jObject["id"]).Value;
                dictionary[$"{_settings.CurrentEntity}:{value}"] = jObject.ToString();
            }

            return dictionary;
        }

        private static Func<JToken, bool> CreateFilterFunction(string filterCondition)
        {
            // You would typically want to add validation and error handling here.
            // For simplicity, this example assumes that the filter condition is valid.

            // Create a parameter expression for the JToken input
            ParameterExpression parameter = Expression.Parameter(typeof(JToken), "item");

            // Create a lambda expression for the filter condition
            Expression filterExpression = DynamicExpressionParser.ParseLambda(
                new[] { parameter }, typeof(bool), filterCondition);

            // Compile the lambda expression into a Func<JToken, bool>
            return Expression.Lambda<Func<JToken, bool>>(filterExpression, parameter).Compile();
        }

        private string CreateFilterExpression(string rawQuery)
        {
            var where = rawQuery.Substring(rawQuery.IndexOf("where") + 5);
            string filterExpression = where.Replace("and", "&&");

            return filterExpression;
        }

        public Task<Dictionary<string, string>> Get(string rawQuery, List<DataFieldsMapping> fieldMappings, string data, int take)
        {
            throw new NotImplementedException();
        }

        public Task<Dictionary<string, string>> Get(string rawQuery, List<DataFieldsMapping> fieldMappings, Dictionary<string, string> data, int take)
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

        public Task Update(JObject entity)
        {
            throw new NotImplementedException();
        }
    }
}