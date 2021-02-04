using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.SqlServer.Server;

namespace Inedo.Data.CodeGenerator
{
    internal sealed class SqlServerConnection : IDatabaseConnection
    {
        public static readonly ConnectToDatabase Connect = cs => new SqlServerConnection(cs);

        private SqlConnection Connection { get; }

        private static readonly Regex DefaultArgRegex = new Regex(@"\bCREATE\s+PROCEDURE\s+[^\(]+\((\s*(?<1>@\S+)\s+[a-zA-Z0-9_]+(\([a-zA-Z0-9,]+\))?(?<2>(\s*=\s*[^\s,\)]+)?)\s*(OUT)?\s*,?)*\)\s*AS\s+BEGIN\b", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Singleline);

        public SqlServerConnection(string connectionString)
        {
            this.Connection = new SqlConnection(connectionString);
        }

        private DataTable ExecuteDataTable(CommandType type, string text)
        {
            var dataTable = new DataTable();

            try
            {
                this.Connection.Open();

                using (var cmd = this.Connection.CreateCommand())
                {
                    cmd.CommandText = text;
                    cmd.CommandType = type;

                    dataTable.Load(cmd.ExecuteReader());
                }
            }
            finally
            {
                this.Connection.Close();
            }

            return dataTable;
        }

        public StoredProcInfo[] GetStoredProcs(string prefix)
        {
            var dataTable = this.ExecuteDataTable(CommandType.Text, $@"
SELECT [StoredProc_Name] = R.ROUTINE_NAME
      ,SPI.[Internal_Indicator]
      ,SPI.[ReturnType_Name]
      ,SPI.[DataTableNames_Csv]
      ,SPI.[OutputPropertyNames_Csv]
      ,SPI.[Description_Text]
      ,SPI.[Remarks_Text]
      ,R.[Routine_Definition]
 FROM INFORMATION_SCHEMA.ROUTINES R
      LEFT JOIN [__{prefix}StoredProcInfo] SPI
             ON R.ROUTINE_NAME = SPI.[StoredProc_Name]
WHERE R.ROUTINE_NAME NOT IN('Events_RaiseEvent', 'HandleError')
  AND R.ROUTINE_TYPE = 'PROCEDURE'
  AND LEFT(R.ROUTINE_NAME, 2) <> '__'
ORDER BY R.ROUTINE_NAME");

            try
            {
                this.Connection.Open();

                return dataTable
                    .Rows
                    .Cast<DataRow>()
                    .Select(r => new StoredProcInfo
                    {
                        Name = r["StoredProc_Name"].ToString(),
                        Description = r["Description_Text"].ToString(),
                        Params = GetParameters(r["StoredProc_Name"].ToString(), r["Routine_Definition"].ToString()).ToArray(),
                        TableNames = r["DataTableNames_Csv"].ToString().Split(',').Select(t => t.Trim()).Where(s => s.Length > 0).ToArray(),
                        OutputPropertyNames = r["OutputPropertyNames_Csv"].ToString().Split(',').Select(p => p.Trim()).Where(s => s.Length > 0).ToArray(),
                        ReturnTypeName = r["ReturnType_Name"].ToString()
                    })
                    .ToArray();
            }
            finally
            {
                this.Connection.Close();
            }
        }
        private IEnumerable<StoredProcParam> GetParameters(string storedProcedureName, string storedProcedureDefinition)
        {
            using (var command = this.Connection.CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = storedProcedureName;

                SqlCommandBuilder.DeriveParameters(command);

                var paramsWithDefaultValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var match = DefaultArgRegex.Match(storedProcedureDefinition);
                if (match.Success)
                {
                    var defaults = match
                        .Groups[1]
                        .Captures
                        .Cast<Capture>()
                        .Zip(
                            match
                                .Groups[2]
                                .Captures
                                .Cast<Capture>(),
                                (c1, c2) => new { Name = c1.Value, HasDefault = !string.IsNullOrWhiteSpace(c2.Value) })
                        .Where(a => a.HasDefault)
                        .Select(a => a.Name);

                    foreach (var arg in defaults)
                        paramsWithDefaultValues.Add(arg);
                }

                foreach (SqlParameter parameter in command.Parameters)
                {
                    if (parameter.ParameterName.Contains("@RETURN"))
                        continue;

                    yield return new StoredProcParam
                    {
                        Name = parameter.ParameterName,
                        Direction = parameter.Direction,
                        DbType = parameter.DbType,
                        Length = parameter.Size,
                        DnType = GetDotNetTypeName(parameter),
                        HasDefault = paramsWithDefaultValues.Contains(parameter.ParameterName)
                    };
                }
            }
        }

        public Dictionary<string, TableInfo> GetTables()
        {
            var getTableNames = "SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME NOT LIKE '!_!_%' ESCAPE '!' ORDER BY TABLE_NAME, ORDINAL_POSITION";
            return this.ExecuteDataTable(CommandType.Text, getTableNames)
                .Rows
                .Cast<DataRow>()
                .GroupBy(r => r["TABLE_NAME"].ToString())
                .Select(t => new TableInfo(t.Key, t.Select(r => new TableColumnInfo { Name = r["COLUMN_NAME"].ToString(), Type = ConvertSqlType(r) })))
                .ToDictionary(t => t.Name);
        }

        public EventTypeInfo[] GetEvents()
        {
            DataTable dataTable;
            try
            {
                dataTable = this.ExecuteDataTable(CommandType.Text, @"SELECT ET.[Event_Code], ET.[Event_Description], [COLUMN_NAME] = ETD.[Detail_Name], [DATA_TYPE] = ETD.[Detail_Type], [IS_NULLABLE] = 'YES'
  FROM [EventTypes] ET
  LEFT JOIN [EventTypeDetails] ETD
         ON ET.[Event_Code] = ETD.[Event_Code]
 ORDER BY ET.[Event_Code], ETD.[Detail_Sequence]");
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return null;
            }

            return dataTable
                .Rows
                .Cast<DataRow>()
                .GroupBy(r => r["Event_Code"].ToString())
                .Select(e => new EventTypeInfo
                {
                    Code = e.Key,
                    Description = e.First()["Event_Description"].ToString(),
                    Details = e.Where(d => d["COLUMN_NAME"] != DBNull.Value).Select(d => new EventTypeDetail
                    {
                        Name = d["COLUMN_NAME"].ToString(),
                        Type = ConvertSqlType(d)
                    })
                    .ToArray()
                })
                .ToArray();
        }

        private static string GetDotNetTypeName(SqlParameter parameter)
        {
            if (parameter.ParameterName.EndsWith("_Indicator"))
                return "YNIndicator?";
            else
                return GetDotNetTypeName(parameter.SqlDbType);
        }
        private static string GetDotNetTypeName(SqlDbType db)
        {
            switch (db)
            {
                case SqlDbType.BigInt:
                    return "long?";
                case SqlDbType.Binary:
                case SqlDbType.Image:
                case SqlDbType.VarBinary:
                    return "byte[]";
                case SqlDbType.Bit:
                    return "bool?";
                case SqlDbType.Char:
                case SqlDbType.NChar:
                case SqlDbType.NText:
                case SqlDbType.NVarChar:
                case SqlDbType.Text:
                case SqlDbType.VarChar:
                case SqlDbType.Xml:
                    return "string";
                case SqlDbType.Date:
                case SqlDbType.DateTime:
                case SqlDbType.DateTime2:
                case SqlDbType.SmallDateTime:
                case SqlDbType.Time:
                case SqlDbType.Timestamp:
                    return "DateTime?";
                case SqlDbType.DateTimeOffset:
                    return "DateTimeOffset?";
                case SqlDbType.Decimal:
                case SqlDbType.Money:
                case SqlDbType.SmallMoney:
                    return "decimal?";
                case SqlDbType.Float:
                    return "double?";
                case SqlDbType.Int:
                case SqlDbType.SmallInt:
                case SqlDbType.TinyInt:
                    return "int?";
                case SqlDbType.Real:
                    return "float?";
                case SqlDbType.UniqueIdentifier:
                    return "Guid?";
                case SqlDbType.Variant:
                    return "object";
                case SqlDbType.Structured:
                    return "IEnumerable<Microsoft.SqlServer.Server.SqlDataRecord>";
                default:
                    return null;
            }
        }
        private static Type GetDotNetType(SqlDbType db)
        {
            switch (db)
            {
                case SqlDbType.BigInt:
                    return typeof(long?);
                case SqlDbType.Binary:
                    return typeof(byte[]);
                case SqlDbType.Bit:
                    return typeof(bool?);
                case SqlDbType.Char:
                    return typeof(string);
                case SqlDbType.Date:
                    return typeof(DateTime?);
                case SqlDbType.DateTime:
                    return typeof(DateTime?);
                case SqlDbType.DateTime2:
                    return typeof(DateTime?);
                case SqlDbType.DateTimeOffset:
                    return typeof(DateTime?);
                case SqlDbType.Decimal:
                    return typeof(decimal?);
                case SqlDbType.Float:
                    return typeof(double?);
                case SqlDbType.Image:
                    return typeof(byte[]);
                case SqlDbType.Int:
                    return typeof(int?);
                case SqlDbType.Money:
                    return typeof(decimal?);
                case SqlDbType.NChar:
                    return typeof(string);
                case SqlDbType.NText:
                    return typeof(string);
                case SqlDbType.NVarChar:
                    return typeof(string);
                case SqlDbType.Real:
                    return typeof(float?);
                case SqlDbType.SmallDateTime:
                    return typeof(DateTime?);
                case SqlDbType.SmallInt:
                    return typeof(int?);
                case SqlDbType.SmallMoney:
                    return typeof(decimal?);
                case SqlDbType.Text:
                    return typeof(string);
                case SqlDbType.Time:
                    return typeof(DateTime?);
                case SqlDbType.Timestamp:
                    return typeof(DateTime?);
                case SqlDbType.TinyInt:
                    return typeof(int?);
                case SqlDbType.UniqueIdentifier:
                    return typeof(Guid?);
                case SqlDbType.VarBinary:
                    return typeof(byte[]);
                case SqlDbType.VarChar:
                    return typeof(string);
                case SqlDbType.Variant:
                    return typeof(object);
                case SqlDbType.Xml:
                    return typeof(string);
                case SqlDbType.Structured:
                    return typeof(IEnumerable<SqlDataRecord>);
                default:
                    return null;
            }
        }
        private static string FormatDotNetType(Type t)
        {
            var type = Nullable.GetUnderlyingType(t) ?? t;
            if (type != t)
                return type.Name + "?";
            else
                return type.Name;
        }

        private static string ConvertSqlType(DataRow column)
        {
            bool nullable = column["IS_NULLABLE"].ToString() == "YES";

            if (column["COLUMN_NAME"].ToString().EndsWith("_Indicator"))
                return nullable ? "YNIndicator?" : "YNIndicator";

            switch (column["DATA_TYPE"].ToString())
            {
                case "int":
                    return nullable ? "int?" : "int";
                case "decimal":
                    return nullable ? "decimal?" : "decimal";
                case "bit":
                    return nullable ? "bool?" : "bool";

                case "text":
                case "ntext":
                case "varchar":
                case "nvarchar":
                case "nchar":
                case "char":
                case "xml":
                    return "string";

                case "date":
                case "datetime":
                case "smalldatetime":
                    return nullable ? "DateTime?" : "DateTime";
                case "datetimeoffset":
                    return nullable ? "DateTimeOffset?" : "DateTimeOffset";

                case "bigint":
                    return nullable ? "long?" : "long";

                case "image":
                case "varbinary":
                case "binary":
                    return "byte[]";

                case "smallint":
                    return nullable ? "short?" : "short";

                case "tinyint":
                    return nullable ? "byte?" : "byte";

                case "sql_variant":
                    return "object";

                case "uniqueidentifier":
                    return nullable ? "Guid?" : "Guid";

                default:
                    throw new NotSupportedException(column["DATA_TYPE"].ToString() + " is not a supported sql datatype");
            }
        }

        void IDisposable.Dispose() => this.Connection.Dispose();
    }
}
