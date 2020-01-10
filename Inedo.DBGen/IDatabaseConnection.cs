using System;
using System.Collections.Generic;

namespace Inedo.Data.CodeGenerator
{
    internal delegate IDatabaseConnection ConnectToDatabase(string connectionString);

    internal interface IDatabaseConnection : IDisposable
    {
        StoredProcInfo[] GetStoredProcs(string prefix);
        Dictionary<string, TableInfo> GetTables();
        EventTypeInfo[] GetEvents();
    }
}
