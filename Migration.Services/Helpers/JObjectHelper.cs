using Newtonsoft.Json.Linq;

namespace Migration.Services.Helpers
{
    public class JObjectHelper
    {
        /// <summary>
        /// Method to validate the property from the JObject and to update with the new values
        /// This method can be recursive in few scenarios (when a property is inside one or more objects), (when the property is inside an array)
        /// </summary>
        /// <param name="json"></param>
        /// <param name="fieldArr"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static JObject UpdateObject(JObject json, List<string> fieldArr, dynamic value)
        {
            var firstProp = fieldArr.FirstOrDefault();

            var fieldArrSize = fieldArr.Count;

            if (fieldArr.Count > 0)
                fieldArr.RemoveAt(0);

            // Check if the token is an object (JObject)

            int? index = 0;

            if (string.IsNullOrEmpty(firstProp)) return json;

            if (firstProp.Contains("[") && firstProp.Contains("]"))
            {
                var firstIndex = firstProp.LastIndexOf("[", StringComparison.Ordinal) + 1;
                var lastIndex = firstProp.IndexOf("]", StringComparison.Ordinal);

                var r = firstProp.Substring(firstIndex, lastIndex - firstIndex);
                index = int.Parse(r);

                firstProp = firstProp.Substring(0, firstIndex - 1);
            }

            json[firstProp] ??= value;

            switch (json[firstProp].Type)
            {
                //Check if the property type is an object, then it need to call recursively to the next level
                case JTokenType.Object:
                    {
                        // It's an object, so you can safely cast it to JObject
                        JObject obj = (JObject)json[firstProp];

                        json[firstProp] = UpdateObject(obj, fieldArr, value);
                        break;
                    }
                //Check if the property type is an array, then it need to call recursively to the next level
                case JTokenType.Array:
                    {
                        // It's an object, so you can safely cast it to JObject
                        var arr = ((JArray)json[firstProp]);

                        if (arr.Count > index)
                        {
                            var jtoken = arr[index];

                            if (jtoken.Type == JTokenType.Object)
                            {
                                if (fieldArr.Count == 0)
                                {
                                    arr[index] = value;
                                }
                                else
                                {
                                    JObject obj = JObject.FromObject(jtoken);
                                    arr[index] = UpdateObject(obj, fieldArr, value);
                                }
                            }
                            else
                            {
                                arr[index] = value;
                            }
                            json[firstProp] = arr;
                        }
                        else
                        {
                            arr.Add(value); //add new index to the array
                            json[firstProp] = arr;
                        }

                        break;
                    }
                default:
                    if (fieldArrSize > 1)
                    {
                        json[firstProp] = UpdateObject(new JObject(), fieldArr, value);
                    }
                    else
                    {
                        ((JValue)json[firstProp]).Value = value;
                    }
                    break;
            }
            return json;
        }

        /// <summary>
        /// Method to validate the property from the JObject and to update with the new values
        /// This method can be recursive in few scenarios (when a property is inside one or more objects), (when the property is inside an array)
        /// </summary>
        /// <param name="json"></param>
        /// <param name="fieldArr"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static JObject UpdateObjectFromOriginal(JObject newData, JObject originalData, List<string> fieldArr, dynamic value)
        {
            var firstProp = fieldArr.FirstOrDefault();

            var fieldArrSize = fieldArr.Count;

            if (fieldArr.Count > 0)
                fieldArr.RemoveAt(0);

            // Check if the token is an object (JObject)

            int? index = 0;

            if (string.IsNullOrEmpty(firstProp)) return newData;

            if (firstProp.Contains("[") && firstProp.Contains("]"))
            {
                var firstIndex = firstProp.LastIndexOf("[", StringComparison.Ordinal) + 1;
                var lastIndex = firstProp.IndexOf("]", StringComparison.Ordinal);

                var r = firstProp.Substring(firstIndex, lastIndex - firstIndex);
                index = int.Parse(r);

                firstProp = firstProp.Substring(0, firstIndex - 1);
            }

            newData[firstProp] ??= value;

            switch (newData[firstProp].Type)
            {
                //Check if the property type is an object, then it need to call recursively to the next level
                case JTokenType.Object:
                    {
                        // It's an object, so you can safely cast it to JObject
                        JObject obj = (JObject)newData[firstProp];

                        if (originalData[firstProp] == null)
                        {
                            if (!originalData.SelectTokens(firstProp).Any())
                            {
                                newData.Remove(firstProp);
                            }
                            else
                            {
                                newData[firstProp] = originalData[firstProp];
                            }
                        }
                        else
                        {
                            newData[firstProp] = UpdateObjectFromOriginal(obj, originalData, fieldArr, value);
                        }

                        break;
                    }
                //Check if the property type is an array, then it need to call recursively to the next level
                case JTokenType.Array:
                    {
                        // It's an object, so you can safely cast it to JObject
                        var arr = ((JArray)newData[firstProp]);

                        if (originalData[firstProp] == null)
                        {
                            arr[index] = originalData[firstProp];
                        }
                        else
                        {
                            if (arr.Count > index)
                            {
                                var jtoken = arr[index];

                                if (jtoken.Type == JTokenType.Object)
                                {
                                    if (fieldArr.Count == 0)
                                    {
                                        arr[index] = value;
                                    }
                                    else
                                    {
                                        JObject obj = JObject.FromObject(jtoken);
                                        arr[index] = UpdateObjectFromOriginal(obj, originalData, fieldArr, value);
                                    }
                                }
                                else
                                {
                                    arr[index] = value;
                                }
                                newData[firstProp] = arr;
                            }
                            else
                            {
                                arr.Add(value); //add new index to the array
                                newData[firstProp] = arr;
                            }
                        }

                        break;
                    }
                default:
                    if (fieldArrSize > 1)
                    {
                        if (originalData[firstProp] == null)
                        {
                            if (!originalData.SelectTokens(firstProp).Any())
                            {
                                newData.Remove(firstProp);
                            }
                            else
                            {
                                newData[firstProp] = originalData[firstProp];
                            }
                        }
                        else
                        {
                            newData[firstProp] = UpdateObjectFromOriginal(new JObject(), originalData, fieldArr, value);
                        }
                    }
                    else
                    {
                        if (!originalData.SelectTokens(firstProp).Any())
                        {
                            newData.Remove(firstProp);
                        }
                        else
                        {
                            ((JValue)newData[firstProp]).Value = value;
                        }
                    }
                    break;
            }
            return newData;
        }

        public static dynamic? GetValueFromObject(JObject json, List<string> fieldArr)
        {
            var firstProp = fieldArr.FirstOrDefault();

            var fieldArrSize = fieldArr.Count;

            if (fieldArr.Count > 0)
                fieldArr.RemoveAt(0);

            dynamic? value = null;
            // Check if the token is an object (JObject)

            int? index = 0;

            if (string.IsNullOrEmpty(firstProp)) return value;

            if (firstProp.Contains("[") && firstProp.Contains("]"))
            {
                var firstIndex = firstProp.LastIndexOf("[", StringComparison.Ordinal) + 1;
                var lastIndex = firstProp.IndexOf("]", StringComparison.Ordinal);

                var r = firstProp.Substring(firstIndex, lastIndex - firstIndex);
                index = int.Parse(r);

                firstProp = firstProp.Substring(0, firstIndex - 1);
            }

            if (json[firstProp] == null)
            {
                return value;
            }

            switch (json[firstProp].Type)
            {
                //Check if the property type is an object, then it need to call recursively to the next level
                case JTokenType.Object:
                    {
                        // It's an object, so you can safely cast it to JObject
                        JObject obj = (JObject)json[firstProp];

                        value = GetValueFromObject(obj, fieldArr);
                        break;
                    }
                //Check if the property type is an array, then it need to call recursively to the next level
                case JTokenType.Array:
                    {
                        // It's an object, so you can safely cast it to JObject
                        var arr = ((JArray)json[firstProp]);

                        if (arr.Count > index)
                        {
                            var jtoken = arr[index];

                            if (jtoken.Type == JTokenType.Object)
                            {
                                JObject obj = JObject.FromObject(jtoken);

                                if (fieldArr.Count == 0)
                                {
                                    value = obj;
                                }
                                else
                                {
                                    value = GetValueFromObject(obj, fieldArr);
                                }
                            }
                            else
                            {
                                value = jtoken.ToString();
                                arr[index] = value;
                            }
                            json[firstProp] = arr;
                        }
                        else
                        {
                            arr.Add(value); //add new index to the array
                            json[firstProp] = arr;
                        }

                        break;
                    }
                default:

                    if (fieldArrSize > 1)
                    {
                        if (json[firstProp] == null)
                        {
                            JObject obj = new JObject();
                            obj.Add(firstProp);
                            value = GetValueFromObject(obj, fieldArr);
                        }
                    }
                    else
                    {
                        value = ((JValue)json[firstProp]).Value;
                    }
                    break;
            }
            return value;
        }
    }
}