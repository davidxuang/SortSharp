using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using SortSharp.Compat;

namespace SortSharp.Foundation;

internal static class Dispatcher<T>
{
    private static readonly bool _isFloatingPointIeee;
    private static readonly IHandler<T> _handler;

    internal static bool IsFloatingPointIeee => _isFloatingPointIeee;
    internal static IHandler<T> To => _handler;

    static Dispatcher()
    {
        _isFloatingPointIeee = typeof(T) == typeof(float) || typeof(T) == typeof(double)
#if NETSTANDARD2_0_COMPAT
            || typeof(T) == typeof(Half)
#endif
#if NET7_0_OR_GREATER
            || (!RuntimeHelpers.IsReferenceOrContainsReferences<T>() && typeof(T).GetInterface(typeof(IFloatingPointIeee754<>).FullName!) is not null)
#endif
            ;

        if (typeof(T) == typeof(byte)) { _handler = (IHandler<T>)(object)new NumberHandler<byte>(); return; }
        if (typeof(T) == typeof(sbyte)) { _handler = (IHandler<T>)(object)new NumberHandler<sbyte>(); return; }
        if (typeof(T) == typeof(short)) { _handler = (IHandler<T>)(object)new NumberHandler<short>(); return; }
        if (typeof(T) == typeof(ushort)) { _handler = (IHandler<T>)(object)new NumberHandler<ushort>(); return; }
        if (typeof(T) == typeof(int)) { _handler = (IHandler<T>)(object)new NumberHandler<int>(); return; }
        if (typeof(T) == typeof(uint)) { _handler = (IHandler<T>)(object)new NumberHandler<uint>(); return; }
        if (typeof(T) == typeof(long)) { _handler = (IHandler<T>)(object)new NumberHandler<long>(); return; }
        if (typeof(T) == typeof(ulong)) { _handler = (IHandler<T>)(object)new NumberHandler<ulong>(); return; }
#if NET5_0_OR_GREATER
        if (typeof(T) == typeof(nint)) { _handler = (IHandler<T>)(object)new NumberHandler<nint>(); return; }
        if (typeof(T) == typeof(nuint)) { _handler = (IHandler<T>)(object)new NumberHandler<nuint>(); return; }
#endif
#if NET7_0_OR_GREATER
        if (typeof(T) == typeof(Int128)) { _handler = (IHandler<T>)(object)new NumberHandler<Int128>(); return; }
        if (typeof(T) == typeof(UInt128)) { _handler = (IHandler<T>)(object)new NumberHandler<UInt128>(); return; }
#endif
        if (typeof(T) == typeof(float)) { _handler = (IHandler<T>)(object)new FloatingPointHandler<float>(); return; }
        if (typeof(T) == typeof(double)) { _handler = (IHandler<T>)(object)new FloatingPointHandler<double>(); return; }
#if NETSTANDARD2_0_COMPAT
        if (typeof(T) == typeof(Half)) { _handler = (IHandler<T>)(object)new FloatingPointHandler<Half>(); return; }
#endif
#if NETSTANDARD2_1_COMPAT
        if (RuntimeFeature.IsDynamicCodeCompiled)
        {
#if NET7_0_OR_GREATER
            if (_isFloatingPointIeee)
            { _handler = (IHandler<T>)Activator.CreateInstance(typeof(FloatingPointHandler<>).MakeGenericType(typeof(T)))!; return; }
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>() && typeof(T).GetInterface(typeof(INumber<>).FullName!) is not null)
            { _handler = (IHandler<T>)Activator.CreateInstance(typeof(NumberHandler<>).MakeGenericType(typeof(T)))!; return; }
#endif
            if (typeof(IComparable<T>).IsAssignableFrom(typeof(T)))
            { _handler = (IHandler<T>)Activator.CreateInstance(typeof(ComparableHandler<>).MakeGenericType(typeof(T)))!; return; }
        }
#else
        try
        {
            if (typeof(IComparable<T>).IsAssignableFrom(typeof(T)))
            { _handler = (IHandler<T>)Activator.CreateInstance(typeof(ComparableHandler<>).MakeGenericType(typeof(T)))!; return; }
        }
        catch { }
#endif
        _handler = new DefaultHandler<T>();
    }
}

internal partial interface IHandler<T>;

internal sealed partial class FloatingPointHandler<T> : IHandler<T>
    where T : unmanaged,
#if NET7_0_OR_GREATER
        IFloatingPointIeee754<T>;
#else
        IComparable<T>;
#endif

internal sealed partial class NumberHandler<T> : IHandler<T>
    where T : unmanaged,
#if NET7_0_OR_GREATER
        INumber<T>;
#else
        IComparable<T>;
#endif

internal sealed partial class ComparableHandler<T> : IHandler<T>
    where T : IComparable<T>;

internal sealed partial class DefaultHandler<T> : IHandler<T>;
