using System;
using System.Collections.Generic;
using Inedo.Data.CodeGenerator.Properties;

namespace Inedo.Data.CodeGenerator
{
    internal sealed class SqlTableGenerator : CodeGeneratorBase
    {
        private readonly Lazy<Dictionary<string, TableInfo>> tables;

        public SqlTableGenerator(Func<string, SqlServerConnection> connect, string connectionString, string baseNamespace)
            : base(connect, connectionString, baseNamespace)
        {
            this.tables = new Lazy<Dictionary<string, TableInfo>>(() =>
            {
                using var connection = this.CreateConnection();
                return connection.GetTables();
            });
        }

        public override string FileName => "Tables.cs";

        private Dictionary<string, TableInfo> Tables => this.tables.Value;

        protected override void WriteBody(IndentingTextWriter writer)
        {
            writer.WriteLine("#pragma warning disable 1591");
            writer.WriteLine("namespace " + this.BaseNamespace);
            writer.WriteLine('{');

            writer.Indent++;
            writer.WriteLine("public static partial class Tables");
            writer.WriteLine("{");

            writer.Indent++;
            foreach (var table in this.Tables.Values)
            {
                if (!table.Name.EndsWith("_Extended") && this.Tables.ContainsKey(table.Name + "_Extended"))
                {
                    if (!Settings.Default.GenerateNonExtendedTables)
                        continue;

                    writer.WriteLine($"[Obsolete(\"{table.Name} is obsolete. Use {table.Name}_Extended instead.\", true)]");
                }

                writer.Write("public partial class " + table.SafeName);

                writer.WriteLine();
                writer.WriteLine('{');
                writer.Indent++;

                foreach (var column in table.Columns)
                {
                    if (column.Name != column.SafeName)
                        writer.WriteLine($"[AmbientValue(\"{column.Name.Replace("\"", "\\\"")}\")]");

                    writer.WriteLine($"public {column.Type} {column.SafeName} {{ get; set; }}");
                }

                writer.Indent--;
                writer.WriteLine('}');
            }

            writer.Indent--;
            writer.WriteLine('}');

            writer.Indent--;
            writer.WriteLine('}');
            writer.WriteLine("#pragma warning restore 1591");
        }
    }
}
