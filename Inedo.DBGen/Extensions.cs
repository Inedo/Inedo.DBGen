using System.Data.SqlClient;

namespace Inedo.Data.CodeGenerator
{
    internal static class Extensions
    {
        public static int? GetNullableInt32(this SqlDataReader reader, int i) => !reader.IsDBNull(i) ? reader.GetInt32(i) : null;
        public static string GetNullableString(this SqlDataReader reader, int i) => !reader.IsDBNull(i) ? reader.GetString(i) : null;
    }
}
