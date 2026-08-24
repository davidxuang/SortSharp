using System;

namespace SortSharp;

/// <remarks><see cref="MemoryProfile.Baseline"/></remarks>
public enum MemoryProfile : sbyte
{
    /// <summary>
    /// Disables temporary buffer allocation.
    /// Throws <see cref="ArgumentException"/> if the sort cannot be completed in-place.
    /// </summary>
    Minimum = sbyte.MinValue,

    /// <summary>
    /// Allows allocating a temporary stack buffer of constant size, independent of the input length.
    /// This is the default profile.
    /// </summary>
    Baseline = 0,

    /// <summary>
    /// Allows allocating a temporary heap buffer whose size is at most sublinear in the input length.
    /// </summary>
    Medium = 61,

    /// <summary>
    /// Allows allocating a temporary heap buffer whose size may be linear in the input length,
    /// when required for performance.
    /// </summary>
    High = 121,
}
