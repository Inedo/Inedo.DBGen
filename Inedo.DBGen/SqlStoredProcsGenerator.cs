using System.Data;
using System.Linq;
using System.Xml.Linq;
using Inedo.Data.CodeGenerator.Properties;

namespace Inedo.Data.CodeGenerator
{
    internal class SqlStoredProcsGenerator : CodeGeneratorBase
    {
        public override bool ShouldGenerate()
        {
            return Settings.Default.GenerateLegacyCodes;
        }

        public SqlStoredProcsGenerator(ConnectToDatabase connect, string connectionString, string baseNamespace, string dataFactoryType, string storedProcPrefix)
            : base(connect, connectionString, baseNamespace)
        {
            this.DataFactoryType = dataFactoryType;
            this.StoredProcPrefix = storedProcPrefix;
        }

        public override string FileName { get { return "StoredProcs.cs"; } }
        public string DataFactoryType { get; private set; }
        public string StoredProcPrefix { get; private set; }

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
            writer.WriteLine("\tpublic class {0} : WrappedStoredProcedure<{1}>", proc.Name, this.DataFactoryType);
            writer.WriteLine("\t{");
            writer.WriteLine("\t\tpublic {0}({1})", proc.Name, string.Join(", ", proc.Params.Select(p => string.Format("{0} {1}", p.DnType, p.Name.TrimStart('@')))));
            writer.WriteLine("\t\t{");
            foreach (StoredProcParam param in proc.Params)
            {
                writer.WriteLine("\t\t\tAddParam(\"{0}\", DbType.{1}, {2}, ParameterDirection.{3}, {4});",
                    param.Name,
                    param.DbType,
                    param.Length,
                    param.Direction,
                    param.DnType == "YNIndicator"
                        ? param.Name.TrimStart('@') + ".ToString()"
                        : param.DnType == "YNIndicator?"
                            ? param.Name.TrimStart('@') + " != null ? " + param.Name.TrimStart('@') + ".ToString() : null"
                            : param.Name.TrimStart('@')
                );
            }
            writer.WriteLine("\t\t}");
            //handle highly heritcal names
            if (proc.IsNameHeretical)
            {
                writer.WriteLine("\t\tprotected override string ProcedureName { get { return \"" + proc.ActualName.Replace(@"""", @"\""") + "\"; } }");
            }
            //here loop again check for ints
            foreach (StoredProcParam param in proc.Params)
            {
                if (param.Direction == ParameterDirection.InputOutput || param.Direction == ParameterDirection.Output)
                {
                    writer.WriteLine();
                    writer.WriteLine("\t\tpublic {0} {1} {{ get {{ return {2}GetParamVal<{0}>(\"@{1}\"); }} }}",
                        param.DnType.StartsWith("YNIndicator") ? "string" : param.DnType,
                        param.Name.Replace("@", ""),
                        param.DnType.StartsWith("YNIndicator") ? "(" + param.DnType + ")" : string.Empty
                    );
                }
            }

            // if there are no tables returned
            if (proc.TableNames.Length == 0)
            {
                // if there is exactly one output property, return it
                if (proc.OutputPropertyNames.Length == 1)
                {
                    var outParam = proc.Params.Where(p => p.Name == "@" + proc.OutputPropertyNames[0]).First();
                    writer.WriteLine("\t\tpublic {0} Execute()", outParam.DnType);
                    writer.WriteLine("\t\t{");
                    writer.WriteLine("\t\t\tthis.ExecuteNonQuery();");
                    writer.WriteLine("\t\t\treturn this.{0};", outParam.Name.TrimStart('@'));
                    writer.WriteLine("\t\t}");
                }
                else // otherwise just return void
                {
                    writer.WriteLine("\t\tpublic void Execute()");
                    writer.WriteLine("\t\t{");
                    writer.WriteLine("\t\t\tthis.ExecuteNonQuery();");
                    writer.WriteLine("\t\t}");
                }
            }
            else if (proc.TableNames.Length == 1 && proc.ReturnTypeName != "DataRow") // if there is exactly one output table
            {
                writer.WriteLine("\t\tpublic IEnumerable<Tables.{0}> Execute()", proc.TableNames[0]);
                writer.WriteLine("\t\t{");
                writer.WriteLine("\t\t\treturn this.ExecuteDataTable().AsStrongTyped<Tables.{0}>();", proc.TableNames[0]);
                writer.WriteLine("\t\t}");

                writer.WriteLine("\t\tpublic IEnumerable<Tables.{0}> Enumerate()", proc.TableNames[0]);
                writer.WriteLine("\t\t{");
                writer.WriteLine("\t\t\treturn this.ExecuteDataReader<Tables.{0}>();", proc.TableNames[0]);
                writer.WriteLine("\t\t}");
            }
            else if (proc.TableNames.Length == 1 && proc.ReturnTypeName == "DataRow") // if there is exactly one output row
            {
                writer.WriteLine("\t\tpublic Tables.{0} Execute()", proc.TableNames[0]);
                writer.WriteLine("\t\t{");
                writer.WriteLine("\t\t\treturn this.ExecuteDataTable().AsStrongTyped<Tables.{0}>().FirstOrDefault();", proc.TableNames[0]);
                writer.WriteLine("\t\t}");
            }
            else // otherwise a dataset is returned
            {
                // write the method
                writer.WriteLine("\t\tpublic {0}_Output Execute()", proc.Name);
                writer.WriteLine("\t\t{");
                writer.WriteLine("\t\t\treturn new {0}_Output(this.ExecuteDataSet({1}));", proc.Name, string.Join(", ", proc.TableNames.Select(t => "\"" + t + "\"")));
                writer.WriteLine("\t\t}");

                // write the output container class
                writer.WriteLine("\t\tpublic sealed class {0}_Output", proc.Name);
                writer.WriteLine("\t\t{");
                writer.WriteLine("\t\t\tprivate DataSet dataSet;");
                writer.WriteLine("\t\t\tinternal {0}_Output(DataSet dataSet) {{ this.dataSet = dataSet; }}", proc.Name);
                foreach (var tableName in proc.TableNames)
                    writer.WriteLine("\t\t\tpublic IEnumerable<Tables.{0}> {0} {{ get {{ return this.dataSet.Tables[\"{0}\"].AsStrongTyped<Tables.{0}>(); }} }}", tableName);
                writer.WriteLine("\t\t}");
            }

            writer.WriteLine("\t}");
            writer.WriteLine();
        }
        private void WriteStaticClasses(IndentingTextWriter writer, StoredProcInfo[] procs)
        {
            writer.WriteLine("namespace {0}", this.BaseNamespace);
            writer.WriteLine("{");
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
                writer.Write(param.Name.TrimStart('@'));
                if (param.HasDefault && (index == proc.Params.Length - 1 || proc.Params.Skip(index + 1).All(p => p.HasDefault)))
                    writer.Write(" = null");

                index++;
            }
            writer.WriteLine(")");

            writer.WriteLine("\t\t{");
            writer.Write("\t\t\t");
            writer.WriteLine(string.Format("return new StoredProcedures.{0}({1});", proc.Name, string.Join(", ", proc.Params.Select(p => p.Name.TrimStart('@')))));
            writer.WriteLine("\t\t}");
            writer.WriteLine();            
        }
    }
}
