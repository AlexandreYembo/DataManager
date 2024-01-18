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

        /// <summary>
        ///
        /// invertedObject means that I am inverting the object that I want to compare but I want to show as the change not the original value
        /// </summary>
        /// <param name="objLeft"></param>
        /// <param name="objRight"></param>
        /// <param name="invertedObject"></param>
        /// <returns></returns>
        public static List<Difference> FindDifferences(JObject objLeft, JObject objRight, bool invertedObject = false, List<DataFieldsMapping> fieldsMappings = null)
        {
            List<Difference> differences = new List<Difference>();

            if (objLeft.Properties().Count() < objRight.Properties().Count())
            {
                return FindDifferences(objRight, objLeft, true, fieldsMappings);
            }

            foreach (var property in objLeft.Properties())
            {
                string propertyName = string.Empty;

                if (fieldsMappings == null)
                {
                    propertyName = property.Name;
                }
                else if(fieldsMappings.Any(a => a.SourceField == property.Name))
                {
                    propertyName = fieldsMappings.FirstOrDefault(a => a.SourceField == property.Name).SourceField;
                }
                else if (fieldsMappings.Any(a => a.DestinationField == property.Name))
                {
                    propertyName = fieldsMappings.FirstOrDefault(a => a.SourceField == property.Name).DestinationField;
                }
                else
                {
                    continue;
                }

                var value1 = invertedObject ? objRight.GetValue(propertyName) : property.Value;
                var value2 = invertedObject ? property.Value : objRight.GetValue(propertyName);

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
                            string v = string.Empty;
                            if (arr1.Count < arr2.Count)
                            {
                                for (int i = arr1.Count; i < arr2.Count; i++)
                                {
                                    v += $"<div>New index for {propertyName} : <span style='color:red'> " + arr2[i] + "</span> </div>";
                                }
                            }
                            differences.Add(new Difference()
                            {
                                PropertyName = propertyName,
                                Object1Value = arr1.ToString(),
                                Object2Value = arr2 + v
                            });

                        }
                        else
                        {
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