
using Migration.Repository.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Repository.Extensions
{
    public static class JObjectExtensions
    {

        public static bool MeetCriteriaSearch(this JObject data, IEnumerable<SearchCondition> condition)
        {
            if (condition.Any(a => a.Type == SearchConditionType.Or))
            {
                return condition.Any(a => MeetCriteriaSearch(data, a.Query));
            }
            return condition.All(a => MeetCriteriaSearch(data, a.Query));
        }

        public static bool MeetCriteriaSearch(this JObject data, string condition)
        {
            condition = String.Join("", condition.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));

            if (condition.Contains("Any"))
            {
                var newCondition = "$." + condition.Trim().Replace(".Any(", "[?(@.")
                    .Replace("&&", " && @.")
                    .Replace("||", " || @.")
                    .Replace(")", ")]");

                bool meetCondition = data.SelectTokens(newCondition).Any();

                return meetCondition;
            }

            var split = condition.Split(new[] { "&&", "||" }, StringSplitOptions.RemoveEmptyEntries);

            var conditionResults = split.Select(s => data.GetValueByType(s.Split("==").FirstOrDefault()) == s.Split("==").LastOrDefault());

            return condition.Contains("||") ? conditionResults.Any(a => a) : conditionResults.All(a => a);

        }

        public static string GetValueByType(this JObject data, string path)
        {
            switch (data.SelectToken(path).Type)
            {
                case JTokenType.Boolean:
                    return data.SelectToken(path).Value<bool>().ToString().ToLower();
                case JTokenType.Float:
                    return data.SelectToken(path).Value<decimal>().ToString();
                case JTokenType.Date:
                    return data.SelectToken(path).Value<DateTime>().ToString();
                case JTokenType.Integer:
                    return data.SelectToken(path).Value<int>().ToString();
                default:
                    return "'" + data.SelectToken(path).Value<string>() + "'";
            }
        }
    }
}