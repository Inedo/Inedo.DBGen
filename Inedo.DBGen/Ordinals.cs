namespace Inedo.Data.CodeGenerator;

internal static class Ordinals
{
    public static class StoredProcs
    {
        public const int ObjectId = 0;
        public const int Name = 1;
        public const int Definition = 2;
        public const int ReturnType = 3;
        public const int DataTableNames = 4;
        public const int Description = 5;
        public const int Remarks = 6;
    }

    public static class StoredProcParams
    {
        public const int ObjectId = 0;
        public const int Name = 1;
        public const int MaxLength = 2;
        public const int Type = 3;
        public const int TableType = 4;
        public const int Output = 5;
    }

    public static class Tables
    {
        public const int TableName = 0;
        public const int ColumnName = 1;
        public const int Nullable = 2;
        public const int DataType = 3;
        public const int MaxLength = 4;
        public const int Precision = 5;
        public const int Scale = 6;
        public const int Uninclused = 7;
    }

    public static class UserDefinedTableColumns
    {
        public const int TableId = 0;
        public const int ColumnName = 1;
        public const int TypeName = 2;
        public const int MaxLength = 3;
        public const int Nullable = 4;
        public const int Precision = 5;
        public const int Scale = 6;
    }

    public static class UserDefinedTables
    {
        public const int TableId = 0;
        public const int TableName = 1;
    }

    public static class ConfigurationColumns
    {
        public const int ColumnName = 0;
        public const int ConfigurationType_Name = 1;
    }
}