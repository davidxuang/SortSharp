using System;
using System.Diagnostics;

namespace SortSharp.SourceGeneration;

#pragma warning disable CS9113

[Conditional("__NEVER__")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
internal sealed class SortAttribute : Attribute
{
    public SortProperties Properties { get; set; }
    public DefaultOverloads Disable { get; set; }
}

[Conditional("__NEVER__")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
internal sealed class SortSpecializationAttribute(string? type = null) : Attribute;

[Flags]
internal enum SortProperties : ushort
{
    NonComparison = 1,
    Stable = 1 << 1,
}
