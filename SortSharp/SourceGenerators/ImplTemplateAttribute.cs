using System;
using System.Diagnostics;

namespace SortSharp.SourceGenerators;

#pragma warning disable CS9113

[Conditional("__NEVER__")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
internal sealed class ImplTemplateAttribute(string type, params string[] identifiers) : Attribute
{
    public OverloadOption KeyValue { get; set; } = OverloadOption.Enable;
    public bool KeySelector { get; set; } = false;

    public string? Comparer { get; set; }
    public ComparerOverloads Disable { get; set; }
    //public OptionalOverloads Enable { get; set; }

    public string? Broadcast { get; set; }
}

[Conditional("__NEVER__")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
internal sealed class ReceiverAttribute(params string[] broadcasts) : Attribute
{
    public string? Type { get; set; }
}

internal enum OverloadOption : byte
{
    Disable,
    Enable,
    Specialized,
}

[Flags]
internal enum ComparerOverloads : byte
{
    Comparison = 1 << 0,
    TComparer = 1 << 1,
    IComparable = 1 << 2,
    IComparisonOperators = 1 << 3,
    All = byte.MaxValue
}

[Flags]
internal enum OptionalOverloads : byte
{
    //LessThanOrEqual = 1 << 0,
}
