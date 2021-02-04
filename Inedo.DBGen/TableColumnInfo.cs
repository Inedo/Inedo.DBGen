namespace Inedo.Data.CodeGenerator
{
    public sealed class TableColumnInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string SafeName => this.Name.Replace(" - ", "_").Replace(" ", "_").Replace("-", "_").Replace(".", "_");
    }
}
