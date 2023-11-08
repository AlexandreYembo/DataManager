using Migration.Repository.Models;
using Newtonsoft.Json.Linq;

namespace Migration.Services.Helpers
{
    public class DifferenceHelper
    {
        public static List<Difference> FindDifferences(List<JObject> list)
        {
            JObject objLeft = JObject.Parse(list[0].ToString());
            JObject objRight = JObject.Parse(list[1].ToString());

            var differences = FindDifferences(objLeft, objRight);

            return differences;
        }

        public static List<Difference> FindDifferences(JObject objLeft, JObject objRight)
        {
            List<Difference> differences = new List<Difference>();

            foreach (var property in objLeft.Properties())
            {
                var propertyName = property.Name;
                var value1 = property.Value;
                var value2 = objRight.GetValue(propertyName);

                if ((value1 != null && value2 == null) || (value1 == null && value2 != null))
                {
                    differences.Add(new Difference()
                    {
                        PropertyName = propertyName,
                        Object1Value = value1 != null ? value1.ToString() : string.Empty,
                        Object2Value = value2 != null ? value2.ToString() : string.Empty
                    });
                }
                else
                {

                    if (value1.Type == JTokenType.Object && value2.Type == JTokenType.Object)
                    {
                        var d = FindDifferences(JObject.Parse(value1.ToString()), JObject.Parse(value2.ToString()));

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

                        if (arr1.Count > arr2.Count || arr1.Count < arr2.Count)
                        {
                            differences.Add(new Difference()
                            {
                                PropertyName = propertyName,
                                Object1Value = arr1.ToString(),
                                Object2Value = arr2.ToString()
                            });

                            return differences;
                        }

                        for (int i = 0; i < arr1.Count; i++)
                        {
                            try
                            {
                                var jtoken1 = arr1[i];
                                var jtoken2 = arr2[i];

                                if (jtoken1.Type == JTokenType.Object && jtoken2.Type == JTokenType.Object)
                                {
                                    var d = FindDifferences(JObject.Parse(jtoken1.ToString()), JObject.Parse(jtoken2.ToString()));

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
                                else
                                {
                                    if (!JToken.DeepEquals(jtoken1.ToString(), jtoken2.ToString()))
                                    {
                                        differences.Add(new Difference()
                                        {
                                            PropertyName = propertyName,
                                            Object1Value = value1.ToString(),
                                            Object2Value = value2.ToString()
                                        });
                                    }
                                }
                            }
                            catch
                            {
                            }
                        }

                    }
                    else if (!JToken.DeepEquals(value1, value2))
                    {
                        differences.Add(new Difference()
                        {
                            PropertyName = propertyName,
                            Object1Value = value1.ToString(),
                            Object2Value = value2.ToString()
                        });
                    }
                }
            }

            return differences;
        }
    }
}