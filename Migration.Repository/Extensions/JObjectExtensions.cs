
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

            var op = string.Empty;

            //add in the future a switch case
            if (split.Any(a => a.Contains("==")))
            {
                op = "==";
            }

            if (split.Any(a => a.Contains("!=")))
            {
                op = "!=";
            }

            var conditionResults = split.Select(s => GetValueByType(data, s.Split(op).FirstOrDefault()) == s.Split(op).LastOrDefault());

            return condition.Contains("||") ? conditionResults.Any(a => a) : conditionResults.All(a => a);
        }

        private static string GetValueByType(JObject data, string value)
        {
            switch (data.SelectToken(value).Type)
            {
                case JTokenType.Boolean:
                    return data.SelectToken(value).Value<bool>().ToString().ToLower();
                case JTokenType.Float:
                    return data.SelectToken(value).Value<decimal>().ToString();
                case JTokenType.Date:
                    return data.SelectToken(value).Value<DateTime>().ToString();
                case JTokenType.Integer:
                    return data.SelectToken(value).Value<int>().ToString();
                default:
                    return data.SelectToken(value).Value<string>();
            }
        }
    }
}