using System;
using System.Diagnostics;

namespace SortSharp.SourceGenerators;

#pragma warning disable CS9113

[Conditional("__NEVER__")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
internal sealed class SortAttribute : Attribute
{
    public SortProperties Properties { get; set; }
    public ComparerOverloads Disable { get; set; }
}

[Flags]
internal enum SortProperties : ushort
{
    Comparison = 1,
    Stable = 1 << 1,
}
