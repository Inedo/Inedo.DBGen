using System;

namespace Inedo.Data.CodeGenerator
{
    internal sealed class SqlEventTypesGenerator : CodeGeneratorBase
    {
        private readonly Lazy<EventTypeInfo[]> eventsLazy;

        public SqlEventTypesGenerator(Func<string, SqlServerConnection> connect, string connectionString, string baseNamespace)
            : base(connect, connectionString, baseNamespace)
        {
            this.eventsLazy = new Lazy<EventTypeInfo[]>(() =>
            {
                using var connection = this.CreateConnection();
                return connection.GetEvents();
            });
        }

        public EventTypeInfo[] Events => this.eventsLazy.Value;
        public override string FileName => "EventTypes.cs";

        public override bool ShouldGenerate() => this.Events != null;

        protected override void WriteBody(IndentingTextWriter writer)
        {
            writer.WriteLine("namespace " + this.BaseNamespace);
            writer.WriteLine("{");
            writer.WriteLine("\tpublic static class EventTypes");
            writer.WriteLine("\t{");

            foreach (var e in this.Events)
            {
                writer.WriteLine("\t\t/// <summary>");
                writer.WriteLine("\t\t/// Represents the {0} event.", e.Description);
                writer.WriteLine("\t\t/// </summary>");
                writer.WriteLine("\t\tpublic sealed class {0} : EventOccurence", e.Code);
                writer.WriteLine("\t\t{");

                writer.WriteLine("\t\t\t/// <summary>");
                writer.WriteLine("\t\t\t/// The event code for this type.");
                writer.WriteLine("\t\t\t/// </summary>");
                writer.WriteLine("\t\t\tpublic const string Event_Code = \"{0}\";", e.Code);
                writer.WriteLine();

                writer.WriteLine("\t\t\t/// <summary>");
                writer.WriteLine("\t\t\t/// Initializes a new instance of the <see cref=\"{0}\"/> class.", e.Code);
                writer.WriteLine("\t\t\t/// </summary>");
                writer.WriteLine("\t\t\tpublic {0}()", e.Code);
                writer.WriteLine("\t\t\t{");
                writer.WriteLine("\t\t\t}");
                writer.WriteLine();

                foreach (var d in e.Details)
                {
                    writer.WriteLine("\t\t\t/// <summary>");
                    writer.WriteLine("\t\t\t/// Gets the value of the {0} event detail.", d.Name);
                    writer.WriteLine("\t\t\t/// </summary>");
                    writer.WriteLine("\t\t\tpublic {0} {1}", d.Type, d.Name);
                    writer.WriteLine("\t\t\t{");
                    writer.WriteLine("\t\t\t\tget {{ return this.GetDetailValue<{0}>(\"{1}\"); }}", d.Type, d.Name);
                    writer.WriteLine("\t\t\t}");
                }

                writer.WriteLine("\t\t}");
                writer.WriteLine();
            }

            writer.WriteLine("\t}");
            writer.WriteLine("}");
        }
    }
}
