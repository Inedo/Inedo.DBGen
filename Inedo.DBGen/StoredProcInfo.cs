using System.Linq;

namespace Inedo.Data.CodeGenerator
{
    public sealed class StoredProcInfo
    {
        private string nonHeritcalName;

        public string Name
        {
            get { return this.nonHeritcalName; }
            set
            {
                this.ActualName = value;
                this.nonHeritcalName = value.Replace(" ", "_").Replace("-", "_");
            }
        }
        public string Description { get; set; }
        public StoredProcParam[] Params { get; set; }
        public string[] TableNames { get; set; }
        public string[] OutputPropertyNames { get; set; }
        public string ReturnTypeName { get; set; }
        public bool IsNameHeretical { get { return this.ActualName != this.nonHeritcalName; } }
        public string ActualName { get; private set; }

        public string FormatParams()
        {
            return string.Join(
                ", ",
                this.Params.Select((p, i) => (p.DnType + " " + p.Name.TrimStart('@')) + (this.Params.Skip(i).All(a => a.HasDefault) ? " = null" : string.Empty))
            );
        }
    }
}
