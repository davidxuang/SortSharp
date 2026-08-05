namespace SortSharp;

public enum MemoryPolicy : sbyte
{
    /// <summary>
    /// Disables temporary buffer allocation.
    /// Throws <see cref="ArgumentException"/> if the sort cannot be completed in-place.
    /// </summary>
    None = sbyte.MinValue,

    /// <summary>
    /// Allows allocating a temporary stack buffer of constant size, independent of the input length.
    /// This is the default policy.
    /// </summary>
    Fixed = 0,

    /// <summary>
    /// Allows allocating a temporary heap buffer whose size is at most sublinear in the input length.
    /// </summary>
    Balanced = 1,

    /// <summary>
    /// Allows allocating a temporary heap buffer whose size may be linear in the input length,
    /// when required for performance.
    /// </summary>
    Maximum = sbyte.MaxValue
}
