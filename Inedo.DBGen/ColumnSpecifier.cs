using System.Diagnostics.CodeAnalysis;

namespace Inedo.Data.CodeGenerator;

internal readonly struct ColumnSpecifier(string table, string column) : IEquatable<ColumnSpecifier>
{
    public string Table { get; } = table;
    public string Column { get; } = column;

    public static ColumnSpecifier Parse(string s)
    {
        var parts = s.Split('.');
        if (parts.Length != 2)
            throw new FormatException("Invalid column specifier.");

        return new ColumnSpecifier(parts[0], parts[1]);
    }

    public bool Equals(ColumnSpecifier other)
    {
        return string.Equals(this.Table, other.Table, StringComparison.OrdinalIgnoreCase)
            && string.Equals(this.Column, other.Column, StringComparison.OrdinalIgnoreCase);
    }
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is ColumnSpecifier cs && this.Equals(cs);
    public override int GetHashCode() => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(this.Table), StringComparer.OrdinalIgnoreCase.GetHashCode(this.Column));
    public override string ToString() => $"{this.Table}.{this.Column}";
}