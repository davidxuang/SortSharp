using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using SortSharp.Compat;

namespace SortSharp.Foundation;

internal ref struct MemoryOwner<T>(IMemoryOwner<T> owner, int? length = null) : IDisposable
{
    private IMemoryOwner<T>? _owner = owner;
    private int? _length = length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IMemoryOwner<T> Attach(IMemoryOwner<T> value)
    {
        if (_owner is not null)
            ThrowHelper.ThrowInvalidOperation("A memory owner is already attached.");
        return _owner = value;
    }

    /// <exception cref="NullReferenceException"/>
    public readonly Memory<T> Memory => _owner!.Memory;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (_owner is null) return;

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            var span = _owner.Memory.Span;
            if (_length is int length)
                span = span.Sub(0, length);
            span.Clear();
        }
        _owner.Dispose();
        _owner = null;
    }
}
