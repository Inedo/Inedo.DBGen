using System;
using System.Data;

namespace Inedo.Data.CodeGenerator
{
    internal readonly struct DataType
    {
        public DataType(string s, int maxLength = -1, bool nullable = false, bool table = false)
        {
            if (Enum.TryParse(s, true, out SqlDbType sqlType))
            {
                this.Name = string.Intern(sqlType.ToString());
                if (HasSize(sqlType))
                    this.MaxLength = IsDoubleSize(sqlType) ? maxLength / 2 : maxLength;
                else
                    this.MaxLength = -1;
            }
            else
            {
                this.Name = string.Intern(s);
                this.MaxLength = -1;
            }

            this.Table = table;
            this.Nullable = nullable;
        }

        public string Name { get; }
        public bool Nullable { get; }
        public bool Table { get; }
        public int MaxLength { get; }

        public override string ToString() => this.Name;

        private static bool HasSize(SqlDbType type)
        {
            return type == SqlDbType.Binary || type == SqlDbType.Char || type == SqlDbType.NChar || type == SqlDbType.NText
                || type == SqlDbType.NVarChar || type == SqlDbType.Text || type == SqlDbType.VarBinary || type == SqlDbType.VarChar;
        }
        private static bool IsDoubleSize(SqlDbType type) => type == SqlDbType.NVarChar || type == SqlDbType.NChar || type == SqlDbType.NText;
    }
}
