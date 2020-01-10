using System.Linq;
using System.Xml.Linq;

namespace Inedo.Data.CodeGenerator
{
    internal class SqlStoredProcsShimGenerator : CodeGeneratorBase
    {
        public override bool ShouldGenerate() => Properties.Settings.Default.GenerateSqlStoredProcsShims;

        public SqlStoredProcsShimGenerator(ConnectToDatabase connect, string connectionString, string baseNamespace, string storedProcPrefix)
            : base(connect, connectionString, baseNamespace)
        {
            this.StoredProcPrefix = storedProcPrefix;
        }

        public override string FileName => "StoredProcShims.cs";
        public string StoredProcPrefix { get; set; }

        protected override void WriteBody(IndentingTextWriter writer)
        {
            StoredProcInfo[] procs;
            using (var connection = this.CreateConnection())
            {
                procs = connection.GetStoredProcs(this.StoredProcPrefix);
            }
            writer.WriteLine("#pragma warning disable 1591");
            WriteSpClasses(writer, procs);
            WriteStaticClasses(writer, procs);
            WriteFooter(writer);
            writer.WriteLine("#pragma warning restore 1591");
        }
        private void WriteSpClasses(IndentingTextWriter writer, StoredProcInfo[] procs)
        {
            writer.WriteLine("namespace {0}.StoredProcedures", this.BaseNamespace);
            writer.WriteLine("{");
            foreach (var proc in procs)
                WriteSpClass(writer, proc);
            writer.WriteLine("}");
        }
        private void WriteSpClass(IndentingTextWriter writer, StoredProcInfo proc)
        {
            writer.WriteLine("\t/// <summary>");
            writer.WriteLine("\t/// " + proc.Description);
            writer.WriteLine("\t/// </summary>");
            writer.WriteLine("\t[EditorBrowsable(EditorBrowsableState.Never)]");
            writer.WriteLine($"\tpublic class {proc.Name}");
            writer.WriteLine("\t{");
            foreach (var param in proc.Params)
            {
                writer.WriteLine($"\t\tprivate {param.DnType} {param.DnName};");
            }
            writer.WriteLine("\t\tpublic {0}({1})", proc.Name, string.Join(", ", proc.Params.Select(p => $"{p.DnType} {p.DnName}")));
            writer.WriteLine("\t\t{");
            foreach (StoredProcParam param in proc.Params)
            {
                writer.WriteLine($"\t\t\tthis.{param.Name.TrimStart('@')} = {param.DnName};");
            }
            writer.WriteLine("\t\t}");

            
            var execParams = string.Join(", ", proc.Params.Select(p => p.DnName));
            var dbExec = $"DB.{proc.Name}({execParams})";

            // if there are no tables returned
            if (proc.TableNames.Length == 0)
            {
                // if there is exactly one output property, return it
                if (proc.OutputPropertyNames.Length == 1)
                {
                    var outParam = proc.Params.Where(p => p.DnName == proc.OutputPropertyNames[0]).First();
                    writer.WriteLine($"\t\tpublic {outParam.DnType} Execute() => {dbExec};");
                }
                else // otherwise just return void
                {
                    writer.WriteLine($"\t\tpublic void Execute() => {dbExec};");
                }
            }
            else if (proc.TableNames.Length == 1 && proc.ReturnTypeName != "DataRow") // if there is exactly one output table
            {
                writer.WriteLine($"\t\tpublic IList<Tables.{proc.TableNames[0]}> Execute() => {dbExec};");
                writer.WriteLine($"\t\tpublic IEnumerable<Tables.{proc.TableNames[0]}> Enumerate() => (new DB.Context(false)).{proc.Name}({execParams});");
            }
            else if (proc.TableNames.Length == 1 && proc.ReturnTypeName == "DataRow") // if there is exactly one output row
            {
                writer.WriteLine($"\t\tpublic Tables.{proc.TableNames[0]} Execute() => {dbExec};");
            }
            else // otherwise a dataset is returned
            {
                writer.WriteLine($"\t\tpublic DB.Outputs.{proc.Name} Execute() => {dbExec};");
            }

            writer.WriteLine("\t}");
            writer.WriteLine();
        }
        private void WriteStaticClasses(IndentingTextWriter writer, StoredProcInfo[] procs)
        {
            writer.WriteLine("namespace {0}", this.BaseNamespace);
            writer.WriteLine("{");
            writer.WriteLine("\t[EditorBrowsable(EditorBrowsableState.Never)]");
            writer.WriteLine("\tpublic static partial class StoredProcs");
            writer.WriteLine("\t{");
            foreach (var proc in procs)
                WriteStaticCreate(writer, proc);
            writer.WriteLine("\t}");
            writer.WriteLine("}");
        }
        private static void WriteStaticCreate(IndentingTextWriter writer, StoredProcInfo proc)
        {
            if (!string.IsNullOrWhiteSpace(proc.Description))
            {
                writer.WriteLine("\t\t/// <summary>");
                writer.WriteLine("\t\t/// " + new XText(proc.Description));
                writer.WriteLine("\t\t/// </summary>");
            }

            int index = 0;
            writer.Write("\t\tpublic static StoredProcedures.{0} {0}(", proc.Name);
            foreach (var param in proc.Params)
            {
                if (index > 0)
                    writer.Write(", ");

                writer.Write(param.DnType);
                writer.Write(' ');
                writer.Write(param.DnName);
                if (param.HasDefault && (index == proc.Params.Length - 1 || proc.Params.Skip(index + 1).All(p => p.HasDefault)))
                    writer.Write(" = null");

                index++;
            }
            writer.Write(") => ");

            writer.WriteLine(string.Format("new StoredProcedures.{0}({1});", proc.Name, string.Join(", ", proc.Params.Select(p => p.DnName))));
            writer.WriteLine();         
        }
    }
}
