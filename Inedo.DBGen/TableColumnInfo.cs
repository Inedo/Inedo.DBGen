namespace Inedo.Data.CodeGenerator
{
    public sealed class TableColumnInfo
    {
        public string Name { get; init; }
        public string Type { get; init; }
        public string SafeName => this.Name.Replace(" - ", "_").Replace(" ", "_").Replace("-", "_").Replace(".", "_");
    }
}
