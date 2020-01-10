using System.Data;

namespace Inedo.Data.CodeGenerator
{
    public struct StoredProcParam
    {
        public string Name;
        public string DnName => this.Name?.TrimStart('@');
        public ParameterDirection Direction;
        public DbType DbType;
        public int Length;
        public string DnType;
        public bool HasDefault;
    }
}
