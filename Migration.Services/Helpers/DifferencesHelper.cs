using Migration.Repository.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Services.Helpers
{
    public class DifferenceHelper
    {
        public static List<Difference> FindDifferences(string json)
        {
            JArray jsonArray = JArray.Parse(json);
            JObject objLeft = JObject.Parse(jsonArray[0].ToString());
            JObject objRight = JObject.Parse(jsonArray[1].ToString());


            List<Difference> differences = new List<Difference>();

            foreach (var property in objLeft.Properties())
            {
                var propertyName = property.Name;
                var value1 = property.Value;
                var value2 = objRight.GetValue(propertyName);

                if (!JToken.DeepEquals(value1, value2))
                {
                    //$"Property '{propertyName}' is different: {value1} != {value2}"
                    differences.Add(new Difference()
                    {
                        PropertyName = propertyName,
                        Object1Value = value1.ToString(),
                        Object2Value = value2.ToString()
                    });
                }
            }

            return differences;
        }

        public static List<Difference> FindDifferences(List<JObject> list)
        {
            JObject objLeft = JObject.Parse(list[0].ToString());
            JObject objRight = JObject.Parse(list[1].ToString());

            var differences = Check(objLeft, objRight);

            return differences;
        }

        private static List<Difference> Check(JObject objLeft, JObject objRight)
        {
            List<Difference> differences = new List<Difference>();

            foreach (var property in objLeft.Properties())
            {
                var propertyName = property.Name;
                var value1 = property.Value;
                var value2 = objRight.GetValue(propertyName);

                if (value1.Type == JTokenType.Object && value2.Type == JTokenType.Object)
                {
                    var d = Check(JObject.Parse(value1.ToString()), JObject.Parse(value2.ToString()));

                    if (d.Any())
                    {
                        var v2 = string.Join("\n ", d.Select(s => $"<div>{s.PropertyName} : <span style='color:red'> " + s.Object2Value + "</span> </div>"));
                        differences.Add(new Difference()
                        {
                            PropertyName = propertyName,
                            Object1Value = value1.ToString(),
                            Object2Value = value2 + v2
                        });
                    }

                }
                else if (value1.Type == JTokenType.Array && value2.Type == JTokenType.Array)
                {
                    var arr1 = JArray.Parse(value1.ToString());
                    var arr2 = JArray.Parse(value2.ToString());

                    for (int i = 0; i < arr1.Count; i++)
                    {
                        try
                        {
                            var d = Check(JObject.Parse(arr1[i].ToString()), JObject.Parse(arr2[i].ToString()));

                            if (d.Any())
                            {
                                var v2 = string.Join("\n", d.Select(s => $"<div>{s.PropertyName} : <span style='color:red'> " + s.Object2Value + "</span> </div>"));

                                differences.Add(new Difference()
                                {
                                    PropertyName = propertyName,
                                    Object1Value = arr1[i].ToString(),
                                    Object2Value = arr2[i] + v2
                                });
                            }
                        }
                        catch
                        {
                        }
                    }

                }
                else if (!JToken.DeepEquals(value1, value2))
                {
                    //$"Property '{propertyName}' is different: {value1} != {value2}"
                    differences.Add(new Difference()
                    {
                        PropertyName = propertyName,
                        Object1Value = value1.ToString(),
                        Object2Value = value2.ToString()
                    });
                }
            }

            return differences;
        }
    }
}