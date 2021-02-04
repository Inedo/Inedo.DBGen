using System.Data;

namespace Inedo.Data.CodeGenerator
{
    public sealed class StoredProcParam
    {
        public string Name { get; init; }
        public string DnName => this.Name?.TrimStart('@');
        public ParameterDirection Direction { get; init; }
        public DbType DbType { get; init; }
        public int Length { get; init; }
        public string DnType { get; init; }
        public bool HasDefault { get; init; }
    }
}
