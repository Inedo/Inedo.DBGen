using System.Linq;

namespace Inedo.Data.CodeGenerator
{
    public sealed class StoredProcInfo
    {
        private string normalizedName;

        public string Name
        {
            get => this.normalizedName;
            init
            {
                this.ActualName = value;
                this.normalizedName = value.Replace(" ", "_").Replace("-", "_");
            }
        }
        public string Description { get; init; }
        public StoredProcParam[] Params { get; init; }
        public string[] TableNames { get; init; }
        public string[] OutputPropertyNames { get; init; }
        public string ReturnTypeName { get; init; }
        public bool IsNameHeretical => this.ActualName != this.normalizedName;
        public string ActualName { get; init; }

        public string FormatParams()
        {
            return string.Join(
                ", ",
                this.Params.Select((p, i) => (p.DnType + " " + p.Name.TrimStart('@')) + (this.Params.Skip(i).All(a => a.HasDefault) ? " = null" : string.Empty))
            );
        }
    }
}
