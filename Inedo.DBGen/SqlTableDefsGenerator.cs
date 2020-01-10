using Inedo.Data.CodeGenerator.Properties;

namespace Inedo.Data.CodeGenerator
{
    internal sealed class SqlTableDefsGenerator : SqlTableGeneratorBase
    {
        public override string FileName { get { return "TableDefs.cs"; } }

        public override bool ShouldGenerate()
        {
            return Settings.Default.GenerateLegacyCodes;
        }

        public SqlTableDefsGenerator(ConnectToDatabase connect, string connectionString, string baseNamespace)
            : base(connect, connectionString, baseNamespace)
        {
        }

        protected override void WriteBody(IndentingTextWriter writer)
        {
            writer.WriteLine("#pragma warning disable 1591");
            writer.WriteLine("namespace " + this.BaseNamespace);
            writer.WriteLine("{");

            writer.WriteLine("\tpublic static class TableDefs");
            writer.WriteLine("\t{");

            foreach (var table in this.Tables.Values)
            {
                if (!table.Name.EndsWith("_Extended") && this.Tables.ContainsKey(table.Name + "_Extended"))
                    writer.WriteLine("\t\t[Obsolete(\"{0} is obsolete. Use {0}_Extended instead.\", true)]", table.Name);
                writer.WriteLine("\t\tpublic static class " + table.SafeName);
                writer.WriteLine("\t\t{");
                foreach (var column in table.Columns)
                    writer.WriteLine("\t\t\tpublic const string {0} = \"{1}\";", column.SafeName, column.Name);
                writer.WriteLine("\t\t}");
            }

            writer.WriteLine("\t}");

            writer.WriteLine("}");
            writer.WriteLine("#pragma warning restore 1591");
        }
    }
}
