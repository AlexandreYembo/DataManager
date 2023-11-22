using Migration.Repository.DbOperations;
using Migration.Repository.Models;

namespace Migration.Repository.Helpers
{
    public class MapFieldTypes
    {
        public static Dictionary<string, dynamic> BuildCommandDictionary(CommandModel s)
        {
            var dic = new Dictionary<string, dynamic>();

            if (string.IsNullOrEmpty(s.Type)) return dic;

            switch (s.Type.ToLower())
            {
                case "integer":
                    dic.Add(s.Field, int.Parse(s.Value));
                    break;

                case "guid":
                    dic.Add(s.Field, Guid.Parse(s.Value));
                    break;
                case "boolean":
                    dic.Add(s.Field, bool.Parse(s.Value));
                    break;

                default:
                    dic.Add(s.Field, s.Value);
                    break;
            }
            return dic;
        }

        public static dynamic GetType(DataFieldsMapping s)
        {
            switch (s.ValueType)
            {
                case FieldValueType.Integer:
                    return int.Parse(s.ValueField);
                case FieldValueType.Guid:
                    return  Guid.Parse(s.ValueField);
                case FieldValueType.Boolean:
                    return bool.Parse(s.ValueField);
                default:
                    return s.ValueField;
            }
        }
    }
}