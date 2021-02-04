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

        public string Name { get; }
        public List<TableColumnInfo> Columns { get;  }
        public string SafeName => this.Name.Replace(" - ", "_").Replace(" ", "_").Replace("-", "_").Replace(".", "_");
    }
}
