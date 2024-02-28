using Migration.Models;
using Migration.Models.Profile;
using Newtonsoft.Json.Linq;

namespace Migration.Services.Extensions
{
    public static class JObjectExtensions
    {

        public static bool MeetCriteriaSearch(this JObject data, IEnumerable<SearchCondition> condition)
        {
            if (!condition.Any()) return true; //when there is not condition means that should not block the upload for the value

            if (condition.Any(a => a.Type == SearchConditionType.Or))
            {
                return condition.Any(a => MeetCriteriaSearch(data, a.Query));
            }
            return condition.All(a => MeetCriteriaSearch(data, a.Query));
        }

        public static bool MeetCriteriaSearch(this JObject data, string conditions)
        {
            //condition = String.Join("", condition.Split(default(string[]), StringSplitOptions.None));
            var split = conditions.Split(new[] { " and ", " or " }, StringSplitOptions.RemoveEmptyEntries);

            var op = string.Empty;

            ////add in the future a switch case
            //if (split.Any(a => a.Contains("==")))
            //{
            //    op = " == ";
            //}

            //if (split.Any(a => a.Contains("!=")))
            //{
            //    op = " != ";
            //}

            List<bool> conditionResults = new();

            foreach (var condition in split)
            {
                //add in the future a switch case
                if (condition.Contains("=="))
                {
                    op = " == ";
                }

                if (condition.Contains("!="))
                {
                    op = " != ";
                }

                if (condition.Contains("Any"))
                {
                    var newCondition = "$." + condition.Replace("\"", "'").Trim()
                        .Replace(".Any(", "[?(@.")
                        .Replace(" and ", " && @.")
                        .Replace(" or ", " || @.")
                        .Replace(")", ")]");

                    bool meetCondition = data.SelectTokens(newCondition).Any();

                    conditionResults.Add(meetCondition);
                }
                else if (condition.Contains("Contains"))
                {
                    conditionResults.Add(data.SelectTokens(condition.Split(".Contains(")
                        .FirstOrDefault()).Values<string>()
                        .Contains(condition.Split(".Contains(").LastOrDefault()
                        .Replace(")", "")
                        .Replace("\"", "")));
                }
                else
                {
                    var property = condition.Split(op).FirstOrDefault();
                    var leftValue = condition.Split(op).LastOrDefault().Replace("\"", "").Replace("'", "");
                    var rightValue = GetValueByType(data, property.Trim());

                    if (condition.Contains("=="))
                    {
                        conditionResults.Add(leftValue == rightValue);
                    }
                    else
                    {
                        conditionResults.Add(leftValue != rightValue);
                    }
                }
            }

            return conditions.Contains(" or ") ? conditionResults.Any(a => a) : conditionResults.All(a => a);
        }

        private static string GetValueByType(JObject data, string value)
        {
            if(data.SelectToken(value) == null)
            {
                return string.Empty;
            }

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
