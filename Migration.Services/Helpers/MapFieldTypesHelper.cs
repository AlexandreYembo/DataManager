using Migration.Models;
using Migration.Models.Profile;

namespace Migration.Services.Helpers
{
    public class MapFieldTypesHelper
    {
        public static dynamic GetType(DataFieldsMapping s)
        {
            switch (s.ValueType)
            {
                case FieldValueType.Integer:
                    return int.Parse(s.ValueField);
                case FieldValueType.Guid:
                    return Guid.Parse(s.ValueField);
                case FieldValueType.Boolean:
                    return bool.Parse(s.ValueField);
                default:
                    return s.ValueField;
            }
        }
    }
}