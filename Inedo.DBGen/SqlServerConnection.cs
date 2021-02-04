using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;

namespace Inedo.Data.CodeGenerator
{
    internal sealed class SqlServerConnection : IDisposable
    {
        private static readonly Regex DefaultArgRegex = new Regex(@"\bCREATE\s+PROCEDURE\s+[^\(]+\((\s*(?<1>@\S+)\s+[a-zA-Z0-9_]+(\([a-zA-Z0-9,]+\))?(?<2>(\s*=\s*[^\s,\)]+)?)\s*(OUT)?\s*,?)*\)\s*AS\s+BEGIN\b", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Singleline);

        public SqlServerConnection(string connectionString)
        {
            this.Connection = new SqlConnection(connectionString);
        }

        private SqlConnection Connection { get; }

        private DataTable ExecuteDataTable(CommandType type, string text)
        {
            var dataTable = new DataTable();

            try
            {
                this.Connection.Open();

                using var cmd = this.Connection.CreateCommand();
                cmd.CommandText = text;
                cmd.CommandType = type;

                dataTable.Load(cmd.ExecuteReader());
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
                        Params = this.GetParameters(r["StoredProc_Name"].ToString(), r["Routine_Definition"].ToString()).ToArray(),
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
            using var command = this.Connection.CreateCommand();
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
            return db switch
            {
                SqlDbType.BigInt => "long?",
                SqlDbType.Binary or SqlDbType.Image or SqlDbType.VarBinary => "byte[]",
                SqlDbType.Bit => "bool?",
                SqlDbType.Char or SqlDbType.NChar or SqlDbType.NText or SqlDbType.NVarChar or SqlDbType.Text or SqlDbType.VarChar or SqlDbType.Xml => "string",
                SqlDbType.Date or SqlDbType.DateTime or SqlDbType.DateTime2 or SqlDbType.SmallDateTime or SqlDbType.Time or SqlDbType.Timestamp => "DateTime?",
                SqlDbType.DateTimeOffset => "DateTimeOffset?",
                SqlDbType.Decimal or SqlDbType.Money or SqlDbType.SmallMoney => "decimal?",
                SqlDbType.Float => "double?",
                SqlDbType.Int or SqlDbType.SmallInt or SqlDbType.TinyInt => "int?",
                SqlDbType.Real => "float?",
                SqlDbType.UniqueIdentifier => "Guid?",
                SqlDbType.Variant => "object",
                SqlDbType.Structured => "IEnumerable<Microsoft.SqlServer.Server.SqlDataRecord>",
                _ => null
            };
        }
        private static string ConvertSqlType(DataRow column)
        {
            bool nullable = column["IS_NULLABLE"].ToString() == "YES";

            if (column["COLUMN_NAME"].ToString().EndsWith("_Indicator"))
                return nullable ? "YNIndicator?" : "YNIndicator";

            return (column["DATA_TYPE"].ToString()) switch
            {
                "int" => nullable ? "int?" : "int",
                "decimal" => nullable ? "decimal?" : "decimal",
                "bit" => nullable ? "bool?" : "bool",
                "text" or "ntext" or "varchar" or "nvarchar" or "nchar" or "char" or "xml" => "string",
                "date" or "datetime" or "smalldatetime" => nullable ? "DateTime?" : "DateTime",
                "datetimeoffset" => nullable ? "DateTimeOffset?" : "DateTimeOffset",
                "bigint" => nullable ? "long?" : "long",
                "image" or "varbinary" or "binary" => "byte[]",
                "smallint" => nullable ? "short?" : "short",
                "tinyint" => nullable ? "byte?" : "byte",
                "sql_variant" => "object",
                "uniqueidentifier" => nullable ? "Guid?" : "Guid",
                _ => throw new NotSupportedException(column["DATA_TYPE"].ToString() + " is not a supported sql datatype")
            };
        }

        void IDisposable.Dispose() => this.Connection.Dispose();
    }
}
