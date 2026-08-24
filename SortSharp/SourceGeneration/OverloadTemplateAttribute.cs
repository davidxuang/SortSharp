using System;
using System.Diagnostics;

namespace SortSharp.SourceGeneration;

#pragma warning disable CS9113

[Conditional("__NEVER__")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
internal sealed class OverloadTemplateAttribute(string type, string? comparer, params string[] identifiers) : Attribute
{
    public DefaultOverloads Disable { get; set; }
    public OptionalOverloads Enable { get; set; }
}

[Flags]
internal enum DefaultOverloads : ushort
{
    KeyValue = 1,
    SiblingSpecializations = 2,

    Comparison = 1 << 8,
    TComparer = 1 << 9,
    IComparable = 1 << 10,
    IComparisonOperators = 1 << 11,
}

[Flags]
internal enum OptionalOverloads : ushort
{
    LessThanOrEqual = 1,
}
