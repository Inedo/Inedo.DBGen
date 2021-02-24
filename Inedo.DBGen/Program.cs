using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Inedo.Data.CodeGenerator.Properties;

namespace Inedo.Data.CodeGenerator
{
    public static class Program
    {
        private static readonly Regex DefaultArgRegex = new Regex(@"\bCREATE\s+PROCEDURE\s+[^\(]+\((\s*(?<1>@\S+)\s+[a-zA-Z0-9_]+(\([a-zA-Z0-9,]+\))?(?<2>(\s*=\s*[^\s,\)]+)?)\s*(OUT)?\s*,?)*\)\s*AS\s+BEGIN\b", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Singleline);

        public static int Main()
        {
            var connectionString = Settings.Default.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Console.Error.WriteLine("ConnectionString not set in config file.");
                return -1;
            }

            Console.Write("Writing DbSchema.xml...");
            WriteSchemaFile("DbSchema.xml", connectionString);
            Console.WriteLine("done");
            return 0;
        }

        private static void WriteSchemaFile(string fileName, string connectionString)
        {
            using var conn = new SqlConnection(connectionString);
            conn.Open();
            using var conn2 = new SqlConnection(connectionString);
            conn2.Open();

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
                WriteTablesXml(writer, reader);
            }

            using (var cmd = new SqlCommand(string.Format(SqlScripts.GetStoredProcsQuery, Settings.Default.StoredProcInfoPrefix), conn))
            using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess))
            {
                WriteStoredProcsXml(writer, reader, conn2);
            }

            writer.WriteEndElement(); // InedoSqlSchema
        }
        private static void WriteTablesXml(XmlWriter writer, SqlDataReader reader)
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
                if (reader.GetString(TableOrdinals.Nullable) == "YES")
                    writer.WriteAttributeString("Nullable", "true");

                writer.WriteAttributeString("Type", reader.GetString(TableOrdinals.DataType));
                int? length = reader.GetNullableInt32(TableOrdinals.MaxLength);
                if (length.HasValue && length != -1)
                    writer.WriteAttributeString("Length", length.GetValueOrDefault().ToString());

                writer.WriteEndElement(); // current column name
            }

            if (currentTableName != null)
                writer.WriteEndElement(); // current table name

            writer.WriteEndElement(); // Tables
        }
        private static void WriteStoredProcsXml(XmlWriter writer, SqlDataReader reader, SqlConnection secondConnection)
        {
            writer.WriteStartElement("StoredProcedures");

            while (reader.Read())
            {
                var storedProcName = reader.GetString(StoredProcOrdinals.StoredProcName);
                writer.WriteStartElement(storedProcName);

                var returnType = reader.GetNullableString(StoredProcOrdinals.ReturnTypeName);
                if (returnType != null && !returnType.Equals("void", StringComparison.OrdinalIgnoreCase))
                    writer.WriteAttributeString("ReturnType", returnType);

                var dataTableNames = reader.GetNullableString(StoredProcOrdinals.DataTableNamesCsv);
                if (!string.IsNullOrWhiteSpace(dataTableNames))
                    writer.WriteAttributeString("OutputTables", dataTableNames.Trim());

                var outputPropertyNames = reader.GetNullableString(StoredProcOrdinals.OutputPropertyNamesCsv);

                var summary = reader.GetNullableString(StoredProcOrdinals.DescriptionText);
                if (!string.IsNullOrWhiteSpace(summary))
                    writer.WriteAttributeString("Summary", summary.Trim());

                var remarks = reader.GetNullableString(StoredProcOrdinals.RemarksText);
                if (!string.IsNullOrWhiteSpace(remarks))
                    writer.WriteAttributeString("Remarks", remarks.Trim());

                var definition = reader.GetString(StoredProcOrdinals.RoutineDefinition);

                using (var command = new SqlCommand(storedProcName, secondConnection) { CommandType = CommandType.StoredProcedure })
                {
                    SqlCommandBuilder.DeriveParameters(command);

                    var returnedOutputs = Array.Empty<string>();
                    if (!string.IsNullOrWhiteSpace(outputPropertyNames))
                        returnedOutputs = outputPropertyNames.Trim().Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();

                    var defaults = GetOptionalParameters(definition);

                    foreach (SqlParameter parameter in command.Parameters)
                    {
                        if (parameter.ParameterName.Equals("@RETURN_VALUE", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var parameterName = parameter.ParameterName.TrimStart('@');
                        writer.WriteStartElement(parameterName);

                        if (parameter.Direction != ParameterDirection.Input)
                            writer.WriteAttributeString("Direction", parameter.Direction.ToString());

                        writer.WriteAttributeString("Type", parameter.SqlDbType.ToString());
                        if (parameter.Size > 0)
                            writer.WriteAttributeString("Length", parameter.Size.ToString());

                        if (Array.IndexOf(returnedOutputs, parameterName) >= 0)
                            writer.WriteAttributeString("Return", "true");

                        if (defaults.Contains(parameterName))
                            writer.WriteAttributeString("Optional", "true");

                        writer.WriteEndElement(); // parameter name
                    }
                }


                writer.WriteEndElement(); // stored proc name
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
