using System.Data;

namespace Inedo.Data.CodeGenerator
{
    public sealed class StoredProcParam
    {
        public string Name { get; set; }
        public string DnName => this.Name?.TrimStart('@');
        public ParameterDirection Direction { get; set; }
        public DbType DbType { get; set; }
        public int Length { get; set; }
        public string DnType { get; set; }
        public bool HasDefault { get; set; }
    }
}
