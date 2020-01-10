namespace Inedo.Data.CodeGenerator
{
    public struct TableColumnInfo
    {
        public string Name;
        public string Type;
        public string SafeName { get { return this.Name.Replace(" - ", "_").Replace(" ", "_").Replace("-", "_").Replace(".", "_"); } }
    }
}
