using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Data.SqlClient;

namespace Inedo.Data.CodeGenerator
{
    public static class Program
    {
        private static readonly Regex DefaultArgRegex = new(@"\bCREATE\s+PROCEDURE\s+[^\(]+\((\s*(?<1>@\S+)\s+[a-zA-Z0-9_]+(\([a-zA-Z0-9,]+\))?(?<2>(\s*=\s*[^\s,\)]+)?)\s*(OUT)?\s*,?)*\)\s*AS\s+BEGIN\b", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Singleline);

        public static int Main(string[] args)
        {
            var connectionString = readArg("connection-string");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.Error.WriteLine("Missing --connection-string=<connection string> argument.");
                return -1;
            }

            Console.Write("Writing DbSchema.xml...");
            WriteSchemaFile("DbSchema.xml", connectionString);
            Console.WriteLine("done");
            return 0;

            string readArg(string name)
            {
                var value = $"--{name}=";

                foreach (var a in args)
                {
                    if(a.StartsWith(value))
                        return a[value.Length..];
                }

                return null;
            }
        }

        private static void WriteSchemaFile(string fileName, string connectionString)
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();

            using var writer = XmlWriter.Create(
                fileName,
                new XmlWriterSettings
                {
                    CloseOutput = true,
                    Encoding = new UTF8Encoding(false),
                    Indent = true,
                    OmitXmlDeclaration = true
                }
            );

            writer.WriteStartElement("InedoSqlSchema");
            writer.WriteAttributeString("GeneratorVersion", typeof(Program).Assembly.GetName().Version.ToString());

            using (var cmd = new SqlCommand(SqlScripts.GetTablesQuery, conn))
            using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess))
            {
                WriteTables(writer, reader);
            }

            var storedProcParams = ReadStoredProcParams(conn);

            using (var cmd = new SqlCommand(SqlScripts.GetStoredProcsQuery, conn))
            using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess))
            {
                WriteStoredProcs(writer, reader, storedProcParams);
            }

            var userTableColumns = ReadUserDefinedTableColumns(conn);

            using (var cmd = new SqlCommand(SqlScripts.GetUserDefinedTables, conn))
            using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess))
            {
                WriteUserDefinedTables(writer, reader, userTableColumns);
            }

            writer.WriteEndElement(); // InedoSqlSchema
        }
        private static void WriteTables(XmlWriter writer, SqlDataReader reader)
        {
            writer.WriteStartElement("Tables");

            string currentTableName = null;

            while (reader.Read())
            {
                // query returns data sorted by table name
                var tableName = reader.GetString(TableOrdinals.TableName);
                if (tableName != currentTableName)
                {
                    if (currentTableName != null)
                        writer.WriteEndElement(); // current table name

                    writer.WriteStartElement(tableName);
                    currentTableName = tableName;
                }

                writer.WriteStartElement(reader.GetString(TableOrdinals.ColumnName));

                bool nullable = reader.GetBoolean(TableOrdinals.Nullable);
                var columnType = reader.GetString(TableOrdinals.DataType);
                int maxLength = reader.GetInt16(TableOrdinals.MaxLength);

                var dataType = new DataType(columnType, maxLength, nullable);

                if (dataType.Nullable)
                    writer.WriteAttributeString("Nullable", "true");

                writer.WriteAttributeString("Type", dataType.ToString());
                if (dataType.MaxLength > 0)
                    writer.WriteAttributeString("Length", dataType.MaxLength.ToString());

                writer.WriteEndElement(); // current column name
            }

            if (currentTableName != null)
                writer.WriteEndElement(); // current table name

            writer.WriteEndElement(); // Tables
        }
        private static Dictionary<int, List<UserDefinedTableTypeColumnInfo>> ReadUserDefinedTableColumns(SqlConnection conn)
        {
            using var cmd = new SqlCommand(SqlScripts.GetUserDefinedTableColumns, conn);
            using var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess);

            var columns = new Dictionary<int, List<UserDefinedTableTypeColumnInfo>>();

            while (reader.Read())
            {
                int tableId = reader.GetInt32(UserDefinedTableColumnOrdinals.TableId);
                var columnName = reader.GetString(UserDefinedTableColumnOrdinals.ColumnName);
                var typeName = reader.GetString(UserDefinedTableColumnOrdinals.TypeName);
                int maxLength = reader.GetInt16(UserDefinedTableColumnOrdinals.MaxLength);
                bool nullable = reader.GetBoolean(UserDefinedTableColumnOrdinals.Nullable);

                if (!columns.TryGetValue(tableId, out var tableColumns))
                {
                    tableColumns = new();
                    columns.Add(tableId, tableColumns);
                }

                tableColumns.Add(new UserDefinedTableTypeColumnInfo(columnName, new DataType(typeName, maxLength, nullable)));
            }

            return columns;
        }
        private static void WriteUserDefinedTables(XmlWriter writer, SqlDataReader reader, Dictionary<int, List<UserDefinedTableTypeColumnInfo>> columnLookup)
        {
            writer.WriteStartElement("TableTypes");

            while (reader.Read())
            {
                int tableId = reader.GetInt32(UserDefinedTableOrdinals.TableId);
                var tableName = reader.GetString(UserDefinedTableOrdinals.TableName);

                writer.WriteStartElement(tableName);

                if (columnLookup.TryGetValue(tableId, out var columns))
                {
                    foreach (var c in columns)
                    {
                        writer.WriteStartElement(c.Name);

                        writer.WriteAttributeString("Type", c.Type.ToString());
                        if (c.Type.MaxLength > 0)
                            writer.WriteAttributeString("Length", c.Type.MaxLength.ToString());
                        if (c.Type.Nullable)
                            writer.WriteAttributeString("Nullable", c.Type.Nullable.ToString());

                        writer.WriteEndElement(); // column name
                    }
                }

                writer.WriteEndElement(); // table name
            }

            writer.WriteEndElement(); // TableTypes
        }
        private static Dictionary<int, List<StoredProcParamInfo>> ReadStoredProcParams(SqlConnection conn)
        {
            using var cmd = new SqlCommand(SqlScripts.GetStoredProcParamsQuery, conn);
            using var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess);

            var parameters = new Dictionary<int, List<StoredProcParamInfo>>();

            while (reader.Read())
            {
                int objectId = reader.GetInt32(StoredProcParamOrdinals.ObjectId);
                var paramName = reader.GetString(StoredProcParamOrdinals.Name);
                int maxLength = reader.GetInt16(StoredProcParamOrdinals.MaxLength);
                var typeName = reader.GetString(StoredProcParamOrdinals.Type);
                bool tableType = reader.GetBoolean(StoredProcParamOrdinals.TableType);
                bool output = reader.GetBoolean(StoredProcParamOrdinals.Output);

                if (!parameters.TryGetValue(objectId, out var paramList))
                {
                    paramList = new();
                    parameters.Add(objectId, paramList);
                }

                paramList.Add(new StoredProcParamInfo(paramName, new DataType(typeName, maxLength, true, tableType), output));
            }

            return parameters;
        }
        private static void WriteStoredProcs(XmlWriter writer, SqlDataReader reader, Dictionary<int, List<StoredProcParamInfo>> paramLookup)
        {
            writer.WriteStartElement("StoredProcedures");

            while (reader.Read())
            {
                int objectId = reader.GetInt32(StoredProcOrdinals.ObjectId);
                var procName = reader.GetString(StoredProcOrdinals.Name);
                var definition = reader.GetNullableString(StoredProcOrdinals.Definition);
                var returnType = reader.GetNullableString(StoredProcOrdinals.ReturnType);
                var dataTableNames = reader.GetNullableString(StoredProcOrdinals.DataTableNames);
                var description = reader.GetNullableString(StoredProcOrdinals.Description);
                var remarks = reader.GetNullableString(StoredProcOrdinals.Remarks);

                var defaults = GetOptionalParameters(definition);

                writer.WriteStartElement(procName);

                if (returnType != null && !returnType.Equals("void", StringComparison.OrdinalIgnoreCase))
                    writer.WriteAttributeString("ReturnType", returnType);

                if (!string.IsNullOrWhiteSpace(dataTableNames))
                    writer.WriteAttributeString("OutputTables", dataTableNames.Trim());

                if (!string.IsNullOrWhiteSpace(description))
                    writer.WriteAttributeString("Summary", description);
                if (!string.IsNullOrWhiteSpace(remarks))
                    writer.WriteAttributeString("Remarks", remarks);

                if (paramLookup.TryGetValue(objectId, out var paramList))
                {
                    foreach (var p in paramList)
                    {
                        writer.WriteStartElement(p.Name);

                        if (p.Output)
                            writer.WriteAttributeString("Direction", "InputOutput");

                        writer.WriteAttributeString("Type", p.Type.ToString());

                        if (p.Type.MaxLength > 0)
                            writer.WriteAttributeString("Size", p.Type.MaxLength.ToString());

                        if (p.Output)
                            writer.WriteAttributeString("Return", "true");

                        if (defaults.Contains(p.Name))
                            writer.WriteAttributeString("Optional", "true");

                        if (p.Type.Table)
                            writer.WriteAttributeString("Table", "true");

                        writer.WriteEndElement(); // param name
                    }
                }

                writer.WriteEndElement(); // proc name
            }

            writer.WriteEndElement(); // StoredProcedures
        }
        private static HashSet<string> GetOptionalParameters(string storedProcDefinition)
        {
            var parameterDefaults = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var match = DefaultArgRegex.Match(storedProcDefinition);
            if (match.Success)
            {
                for (int i = 0; i < match.Groups[1].Captures.Count; i++)
                {
                    var name = match.Groups[1].Captures[i].Value.TrimStart('@');
                    var defaultValue = match.Groups[2].Captures[i].Value;
                    if (!string.IsNullOrWhiteSpace(defaultValue))
                        parameterDefaults.Add(name);
                }
            }

            return parameterDefaults;
        }
    }
}
