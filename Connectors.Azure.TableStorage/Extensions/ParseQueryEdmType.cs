using Microsoft.WindowsAzure.Storage.Table;
using System.Globalization;

namespace Connectors.Azure.TableStorage.Extensions
{
    public static class ParseQueryEdmType
    {
        public static string ParseToString(this EdmType edmType, string propertyName, string operation, string value)
        {
            string str = "";
            switch (edmType)
            {
                case EdmType.Binary:
                    str = string.Format(CultureInfo.InvariantCulture, "X'{0}'", value);
                    break;
                case EdmType.Boolean:
                case EdmType.Int32:
                    str = value;
                    break;
                case EdmType.DateTime:
                    str = string.Format(CultureInfo.InvariantCulture, "datetime'{0}'", value);
                    break;
                case EdmType.Double:
                    str = int.TryParse(value, out int _) ? string.Format(CultureInfo.InvariantCulture, "{0}.0", (object)value) : value;
                    break;
                case EdmType.Guid:
                    str = string.Format(CultureInfo.InvariantCulture, "guid'{0}'", value);
                    break;
                case EdmType.Int64:
                    str = string.Format(CultureInfo.InvariantCulture, "{0}L", value);
                    break;
                default:
                    str = string.Format(CultureInfo.InvariantCulture, "'{0}'", value.Replace("'", "''"));
                    break;
            }
            return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", propertyName, operation, str);
        }
    }
}