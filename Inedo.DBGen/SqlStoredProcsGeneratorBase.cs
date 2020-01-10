using System;

namespace Inedo.Data.CodeGenerator
{
    internal abstract class SqlStoredProcsGeneratorBase : CodeGeneratorBase
    {
        private readonly Lazy<StoredProcInfo[]> storedProcsLazy;

        public SqlStoredProcsGeneratorBase(ConnectToDatabase connect, string connectionString, string baseNamespace, string storedProcPrefix)
            : base(connect, connectionString, baseNamespace)
        {
            this.storedProcsLazy = new Lazy<StoredProcInfo[]>(() =>
            {
                using (var connection = this.CreateConnection())
                {
                    return connection.GetStoredProcs(storedProcPrefix);
                }
            });
        }

        public StoredProcInfo[] StoredProcs => this.storedProcsLazy.Value;
    }
}
