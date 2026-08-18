using System;   

namespace SortSharp.SourceGeneration;

[Flags]
internal enum TemplateVariants : uint
{
    Comparison = 0,
    // IComparer = 1, // redirected to (TComparer = IComparer<T>)
    TComparer = 1,
    IComparable = 2,
    IComparisonOperators = 4,

    KeyValue = 1u << 8,

    /// <summary>
    /// Excluded by default.
    /// </summary>
    LessThanOrEqual = 1u << 16,
}
