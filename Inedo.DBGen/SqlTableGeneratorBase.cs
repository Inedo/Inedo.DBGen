using System;
using System.Collections.Generic;

namespace Inedo.Data.CodeGenerator
{
    internal abstract class SqlTableGeneratorBase : CodeGeneratorBase
    {
        private readonly Lazy<Dictionary<string, TableInfo>> tables;

        public SqlTableGeneratorBase(ConnectToDatabase connect, string connectionString, string baseNamespace)
            : base(connect, connectionString, baseNamespace)
        {
            this.tables = new Lazy<Dictionary<string, TableInfo>>(() =>
            {
                using (var connection = this.CreateConnection())
                {
                    return connection.GetTables();
                }
            });
        }

        protected Dictionary<string, TableInfo> Tables => this.tables.Value;
    }
}
