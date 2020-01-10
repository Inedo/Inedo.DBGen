using System.Collections.Generic;

namespace Inedo.Data.CodeGenerator
{
    public sealed class TableInfo
    {
        public TableInfo(string name, IEnumerable<TableColumnInfo> columns)
        {
            this.Columns = new List<TableColumnInfo>(columns);
            this.Name = name;
        }

        public string Name { get; private set; }
        public List<TableColumnInfo> Columns { get; private set; }
        public string SafeName { get { return this.Name.Replace(" - ", "_").Replace(" ", "_").Replace("-", "_").Replace(".", "_"); } }
    }
}
