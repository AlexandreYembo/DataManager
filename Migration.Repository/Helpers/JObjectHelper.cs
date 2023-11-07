using Newtonsoft.Json.Linq;

namespace Migration.Repository.Helpers
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
        public static JObject GetObject(JObject json, List<string> fieldArr, dynamic value)
        {
            var firstProp = fieldArr.FirstOrDefault();

            if (fieldArr.Count > 0)
                fieldArr.RemoveAt(0);

            // Check if the token is an object (JObject)

            int? index = 0;

            if (firstProp == null) return json;

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

                        json[firstProp] = GetObject(obj, fieldArr, value);
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
                                arr[index] = GetObject(obj, fieldArr, value);
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
                    ((JValue)json[firstProp]).Value = value;
                    break;
            }
            return json;
        }

        public static dynamic? GetValueFromObject(JObject json, List<string> fieldArr)
        {
            var firstProp = fieldArr.FirstOrDefault();
            fieldArr.RemoveAt(0);
            dynamic? value = null;
            // Check if the token is an object (JObject)

            int? index = 0;

            if (firstProp == null) return value;

            if (firstProp.Contains("[") && firstProp.Contains("]"))
            {
                var firstIndex = firstProp.LastIndexOf("[", StringComparison.Ordinal) + 1;
                var lastIndex = firstProp.IndexOf("]", StringComparison.Ordinal);

                var r = firstProp.Substring(firstIndex, lastIndex - firstIndex);
                index = int.Parse(r);

                firstProp = firstProp.Substring(0, firstIndex - 1);
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
                                arr[index] = GetValueFromObject(obj, fieldArr);
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
                    value = ((JValue)json[firstProp]).Value;
                    break;
            }
            return value;
        }
    }
}