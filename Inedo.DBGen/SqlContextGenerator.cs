using System;
using System.Linq;
using System.Xml.Linq;

namespace Inedo.Data.CodeGenerator
{
    internal class SqlContextGenerator : SqlStoredProcsGeneratorBase
    {
        public SqlContextGenerator(ConnectToDatabase connect, string connectionString, string baseNamespace, string dataContextType, string storedProcPrefix)
            : base(connect, connectionString, baseNamespace, storedProcPrefix)
        {
            this.DataContextType = dataContextType;
        }

        public override bool ShouldGenerate() => !string.IsNullOrEmpty(this.DataContextType);

        public override string FileName => "DB.cs";
        public string DataContextType { get; }

        protected override void WriteBody(IndentingTextWriter writer)
        {
            writer.WriteLine("#pragma warning disable 1591");
            writer.WriteLine("namespace " + this.BaseNamespace);
            writer.WriteLine('{');
            writer.Indent++;

            this.WriteDBClass(writer, this.StoredProcs);

            writer.Indent--;
            writer.WriteLine('}');

            this.WriteFooter(writer);
            writer.WriteLine("#pragma warning restore 1591");
        }

        private void WriteDBClass(IndentingTextWriter writer, StoredProcInfo[] procs)
        {
            writer.WriteLine("/// <summary>");
            writer.WriteLine("/// Provides strongly typed wrapper methods for stored procedures.");
            writer.WriteLine("/// </summary>");
            writer.WriteLine("public static partial class DB");
            writer.WriteLine('{');
            writer.Indent++;

            foreach (var proc in procs)
                this.WriteSpMethod(writer, proc, true);

            this.WriteContextClass(writer, procs);

            this.WriteSpOutputsClass(writer, procs);

            writer.Indent--;
            writer.WriteLine('}');
        }
        private void WriteContextClass(IndentingTextWriter writer, StoredProcInfo[] procs)
        {
            writer.WriteLine("/// <summary>");
            writer.WriteLine("/// Provides a strongly typed context wrapper to allow for transactions and row enumeration.");
            writer.WriteLine("/// </summary>");
            writer.WriteLine("public sealed partial class Context : " + this.DataContextType);
            writer.WriteLine('{');
            writer.Indent++;

            writer.WriteLine("/// <summary>");
            writer.WriteLine("/// Initializes a new instance of the <see cref=\"Context\"/> class.");
            writer.WriteLine("/// </summary>");
            writer.WriteLine("public Context() { }");
            writer.WriteLine("/// <summary>");
            writer.WriteLine("/// Initializes a new instance of the <see cref=\"Context\"/> class.");
            writer.WriteLine("/// </summary>");
            writer.WriteLine("/// <param name=\"keepConnection\">Value indicating whether to maintain an open connection between commands.</param>");
            writer.WriteLine("public Context(bool keepConnection) : base(keepConnection) { }");
            writer.WriteLine();

            foreach (var proc in procs)
            {
                this.WriteSpMethod(writer, proc);
                this.WriteAsyncInstanceMethod(writer, proc);
            }

            writer.Indent--;
            writer.WriteLine('}');
        }
        private void WriteSpMethod(IndentingTextWriter writer, StoredProcInfo proc, bool staticMethod = false)
        {
            var returnType = GetReturnType(proc, staticMethod);

            writer.WriteLine("/// <summary>");
            writer.WriteLine("/// " + new XText(proc.Description));
            writer.WriteLine("/// </summary>");
            writer.Write("public ");
            if (staticMethod)
                writer.Write("static ");
            writer.Write(returnType);
            writer.Write(' ');
            writer.Write(proc.Name);
            writer.Write('(');
            writer.Write(proc.FormatParams());
            writer.WriteLine(')');

            writer.WriteLine('{');
            writer.Indent++;
            if (staticMethod)
            {
                if (returnType != "void")
                    writer.Write("return ");

                writer.WriteLine("new Context(false).{0}({1}){2};",
                    proc.Name,
                    string.Join(", ", proc.Params.Select(p => p.Name.TrimStart('@'))),
                    returnType.StartsWith("IList<") ? ".ToList()" : ""
                );
            }
            else
            {
                writer.WriteLine($"var list = new GenericDbParameter[{proc.Params.Length}]");
                writer.WriteLine('{');
                writer.Indent++;
                bool first = true;
                foreach (var param in proc.Params)
                {
                    if (!first)
                        writer.WriteLine(',');
                    else
                        first = false;

                    if (param.DnType == "IEnumerable<Microsoft.SqlServer.Server.SqlDataRecord>")
                    {
                        writer.Write($"new GenericDbParameter(\"{param.Name}\", {param.Name.TrimStart('@')}");
                    }
                    else
                    {
                        writer.Write($"new GenericDbParameter(\"{param.Name}\", DbType.{param.DbType}, {param.Length}, ParameterDirection.{param.Direction}, ");
                        if (param.DnType == "YNIndicator")
                            writer.Write(param.Name.TrimStart('@') + ".ToString()");
                        else if (param.DnType == "YNIndicator?")
                            writer.Write(param.Name.TrimStart('@') + "?.ToString()");
                        else
                            writer.Write(param.Name.TrimStart('@'));
                    }

                    writer.Write(')');
                }

                if (proc.Params.Length > 0)
                    writer.WriteLine();

                writer.Indent--;
                writer.WriteLine("};");

                // if there are no tables returned
                if (proc.TableNames.Length == 0)
                {
                    writer.WriteLine($"this.ExecuteNonQuery(\"{proc.Name}\", list);");

                    // if there is exactly one output property, return it
                    if (proc.OutputPropertyNames.Length == 1)
                    {
                        var outParam = proc.Params.Where(p => p.Name == "@" + proc.OutputPropertyNames[0]).First();

                        writer.WriteLine($"if (Convert.IsDBNull(list[{Array.IndexOf(proc.Params, outParam)}].Value))");
                        writer.Indent++;
                        writer.WriteLine($"return default({outParam.DnType});");
                        writer.Indent--;
                        writer.WriteLine("else");
                        writer.Indent++;
                        writer.Write($"return ({outParam.DnType})");
                        if (outParam.DnType == "YNIndicator" || outParam.DnType == "YNIndicator?")
                            writer.Write("(string)");

                        writer.WriteLine($"list[{Array.IndexOf(proc.Params, outParam)}].Value;");

                        writer.Indent--;
                    }
                }
                else if (proc.TableNames.Length == 1 && proc.ReturnTypeName != "DataRow") // if there is exactly one output table
                {
                    writer.WriteLine($"return this.EnumerateTable<Tables.{proc.TableNames[0]}>(\"{proc.Name}\", list);");
                }
                else if (proc.TableNames.Length == 1 && proc.ReturnTypeName == "DataRow") // if there is exactly one output row
                {
                    writer.WriteLine($"return this.EnumerateTable<Tables.{proc.TableNames[0]}>(\"{proc.Name}\", list).FirstOrDefault();");
                }
                else // otherwise a dataset is returned
                {
                    returnType = "DB.Outputs." + proc.Name;

                    writer.WriteLine($"var r = new {returnType}();");

                    writer.WriteLine($"using (var d = this.Execute(\"{proc.Name}\", list))");
                    writer.WriteLine('{');

                    writer.Indent++;
                    foreach (var name in proc.TableNames)
                    {
                        writer.WriteLine($"r.{name} = StrongDataReader.Read<Tables.{name}>(d).ToList();");
                        writer.WriteLine("d.Reader.NextResult();");
                    }

                    writer.Indent--;
                    writer.WriteLine('}');

                    writer.WriteLine("return r;");
                }
            }

            writer.Indent--;
            writer.WriteLine('}');
        }

        private void WriteSpOutputsClass(IndentingTextWriter writer, StoredProcInfo[] procs)
        {
            writer.WriteLine("public static class Outputs");
            writer.WriteLine('{');
            writer.Indent++;

            foreach (var proc in procs.Where(p => p.TableNames.Length > 1))
            {
                // write the output container class
                writer.WriteLine("public sealed class " + proc.Name);
                writer.WriteLine('{');
                writer.Indent++;

                foreach (var tableName in proc.TableNames)
                    writer.WriteLine("public IList<Tables.{0}> {0} {{ get; set; }}", tableName);

                writer.Indent--;
                writer.WriteLine('}');
            }

            writer.Indent--;
            writer.WriteLine('}');
        }

        private void WriteAsyncInstanceMethod(IndentingTextWriter writer, StoredProcInfo proc)
        {
            var returnType = GetReturnType(proc, true);

            writer.WriteLine("/// <summary>");
            writer.WriteLine("/// " + new XText(proc.Description));
            writer.WriteLine("/// </summary>");
            writer.Write("public ");

            bool isAsyncMethod = (proc.TableNames.Length == 1 && proc.ReturnTypeName == "DataRow") || proc.TableNames.Length > 1;

            if (isAsyncMethod)
                writer.Write("async ");

            if (returnType != "void")
                writer.Write($"Task<{returnType}>");
            else
                writer.Write("Task");
            writer.Write(' ');
            writer.Write(proc.Name + "Async");
            writer.Write('(');
            writer.Write(proc.FormatParams());
            writer.WriteLine(')');

            writer.WriteLine('{');
            writer.Indent++;

            writer.WriteLine($"var list = new GenericDbParameter[{proc.Params.Length}]");
            writer.WriteLine('{');
            writer.Indent++;
            bool first = true;
            foreach (var param in proc.Params)
            {
                if (!first)
                    writer.WriteLine(',');
                else
                    first = false;

                if (param.DnType == "IEnumerable<Microsoft.SqlServer.Server.SqlDataRecord>")
                {
                    writer.Write($"new GenericDbParameter(\"{param.Name}\", {param.Name.TrimStart('@')}");
                }
                else
                {
                    writer.Write($"new GenericDbParameter(\"{param.Name}\", DbType.{param.DbType}, {param.Length}, ParameterDirection.{param.Direction}, ");
                    if (param.DnType == "YNIndicator")
                        writer.Write(param.Name.TrimStart('@') + ".ToString()");
                    else if (param.DnType == "YNIndicator?")
                        writer.Write(param.Name.TrimStart('@') + "?.ToString()");
                    else
                        writer.Write(param.Name.TrimStart('@'));
                }

                writer.Write(')');
            }

            if (proc.Params.Length > 0)
                writer.WriteLine();

            writer.Indent--;
            writer.WriteLine("};");

            // if there are no tables returned
            if (proc.TableNames.Length == 0)
            {
                // if there is exactly one output property, return it
                if (proc.OutputPropertyNames.Length == 1)
                {
                    var outParam = proc.Params.Where(p => p.Name == "@" + proc.OutputPropertyNames[0]).First();
                    writer.WriteLine($"return this.ExecuteScalarAsync<{outParam.DnType}>(\"{proc.Name}\", list, {Array.IndexOf(proc.Params, outParam)});");
                }
                else
                {
                    writer.WriteLine($"return this.ExecuteNonQueryAsync(\"{proc.Name}\", list);");
                }
            }
            else if (proc.TableNames.Length == 1 && proc.ReturnTypeName != "DataRow") // if there is exactly one output table
            {
                writer.WriteLine($"return this.ExecuteTableAsync<Tables.{proc.TableNames[0]}>(\"{proc.Name}\", list);");
            }
            else if (proc.TableNames.Length == 1 && proc.ReturnTypeName == "DataRow") // if there is exactly one output row
            {
                writer.WriteLine($"return (await this.ExecuteTableAsync<Tables.{proc.TableNames[0]}>(\"{proc.Name}\", list).ConfigureAwait(false)).FirstOrDefault();");
            }
            else // otherwise a dataset is returned
            {
                returnType = "DB.Outputs." + proc.Name;

                writer.WriteLine($"var r = new {returnType}();");

                writer.WriteLine($"using (var d = await this.ExecuteAsync(\"{proc.Name}\", list).ConfigureAwait(false))");
                writer.WriteLine('{');

                writer.Indent++;
                foreach (var name in proc.TableNames)
                {
                    writer.WriteLine($"r.{name} = await StrongDataReader.ReadAllAsync<Tables.{name}>(d).ConfigureAwait(false);");
                    writer.WriteLine("await d.Reader.NextResultAsync().ConfigureAwait(false);");
                }

                writer.Indent--;
                writer.WriteLine('}');

                writer.WriteLine("return r;");
            }

            writer.Indent--;
            writer.WriteLine('}');
        }

        private static string GetReturnType(StoredProcInfo proc, bool staticMethod)
        {
            string returnType;
            if (proc.TableNames.Length == 0)
            {
                if (proc.OutputPropertyNames.Length == 1)
                {
                    var outParam = proc.Params.Where(p => p.Name == "@" + proc.OutputPropertyNames[0]).First();
                    returnType = outParam.DnType;
                }
                else
                {
                    returnType = "void";
                }
            }
            else if (proc.TableNames.Length == 1 && proc.ReturnTypeName != "DataRow") // if there is exactly one output table
            {
                if (staticMethod)
                    returnType = $"IList<Tables.{proc.TableNames[0]}>";
                else
                    returnType = $"IEnumerable<Tables.{proc.TableNames[0]}>";
            }
            else if (proc.TableNames.Length == 1 && proc.ReturnTypeName == "DataRow") // if there is exactly one output row
            {
                returnType = "Tables." + proc.TableNames[0];
            }
            else // otherwise a dataset is returned
            {
                returnType = "DB.Outputs." + proc.Name;
            }

            return returnType;
        }
    }
}
