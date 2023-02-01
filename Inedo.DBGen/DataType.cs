namespace Inedo.Data.CodeGenerator;

internal readonly struct DataType
{
    public DataType(string s, int maxLength = -1, bool nullable = false, bool table = false, int scale = -1, int precision = -1)
    {
        if (Enum.TryParse(s, true, out SqlDbType sqlType))
        {
            this.Name = string.Intern(sqlType.ToString());
            if (HasSize(sqlType))
                this.MaxLength = IsDoubleSize(sqlType) ? maxLength / 2 : maxLength;
            else
                this.MaxLength = -1;

            if (sqlType == SqlDbType.Decimal && scale >= 0 && precision >= 0)
            {
                this.Scale = scale;
                this.Precision = precision;
            }
        }
        else
        {
            this.Name = string.Intern(s);
            this.MaxLength = -1;
            this.Scale = -1;
            this.Precision = -1;
        }

        this.Table = table;
        this.Nullable = nullable;
    }

    public string Name { get; }
    public bool Nullable { get; }
    public bool Table { get; }
    public int MaxLength { get; }
    public int Scale { get; }
    public int Precision { get; }

    public override string ToString() => this.Name;

    private static bool HasSize(SqlDbType type)
    {
        return type is SqlDbType.Binary or SqlDbType.Char or SqlDbType.NChar or SqlDbType.NText
            or SqlDbType.NVarChar or SqlDbType.Text or SqlDbType.VarBinary or SqlDbType.VarChar;
    }
    private static bool IsDoubleSize(SqlDbType type) => type is SqlDbType.NVarChar or SqlDbType.NChar or SqlDbType.NText;
}
