using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using SortSharp.Extensions;

namespace SortSharp;

internal static class Router<T>
{
    private static readonly bool _isFloatingPointIeee;
    private static readonly IDispatcher<T> _dispatcher;

    internal static bool IsFloatingPointIeee => _isFloatingPointIeee;
    internal static IDispatcher<T> To => _dispatcher;

    static Router()
    {
        _isFloatingPointIeee = typeof(T) == typeof(float) || typeof(T) == typeof(double)
#if NETSTANDARD2_0_COMPAT
            || typeof(T) == typeof(Half)
#endif
#if NET7_0_OR_GREATER
            || (!RuntimeHelpers.IsReferenceOrContainsReferences<T>() && typeof(T).GetInterface(typeof(IFloatingPointIeee754<>).FullName!) is not null)
#endif
            ;

        if (typeof(T) == typeof(byte)) { _dispatcher = (IDispatcher<T>)(object)new NumberDispatcher<byte>(); return; }
        if (typeof(T) == typeof(sbyte)) { _dispatcher = (IDispatcher<T>)(object)new NumberDispatcher<sbyte>(); return; }
        if (typeof(T) == typeof(short)) { _dispatcher = (IDispatcher<T>)(object)new NumberDispatcher<short>(); return; }
        if (typeof(T) == typeof(ushort)) { _dispatcher = (IDispatcher<T>)(object)new NumberDispatcher<ushort>(); return; }
        if (typeof(T) == typeof(int)) { _dispatcher = (IDispatcher<T>)(object)new NumberDispatcher<int>(); return; }
        if (typeof(T) == typeof(uint)) { _dispatcher = (IDispatcher<T>)(object)new NumberDispatcher<uint>(); return; }
        if (typeof(T) == typeof(long)) { _dispatcher = (IDispatcher<T>)(object)new NumberDispatcher<long>(); return; }
        if (typeof(T) == typeof(ulong)) { _dispatcher = (IDispatcher<T>)(object)new NumberDispatcher<ulong>(); return; }
#if NET5_0_OR_GREATER
        if (typeof(T) == typeof(nint)) { _dispatcher = (IDispatcher<T>)(object)new NumberDispatcher<nint>(); return; }
        if (typeof(T) == typeof(nuint)) { _dispatcher = (IDispatcher<T>)(object)new NumberDispatcher<nuint>(); return; }
#endif
#if NET7_0_OR_GREATER
        if (typeof(T) == typeof(Int128)) { _dispatcher = (IDispatcher<T>)(object)new NumberDispatcher<Int128>(); return; }
        if (typeof(T) == typeof(UInt128)) { _dispatcher = (IDispatcher<T>)(object)new NumberDispatcher<UInt128>(); return; }
#endif
        if (typeof(T) == typeof(float)) { _dispatcher = (IDispatcher<T>)(object)new FloatingPointDispatcher<float>(); return; }
        if (typeof(T) == typeof(double)) { _dispatcher = (IDispatcher<T>)(object)new FloatingPointDispatcher<double>(); return; }
#if NETSTANDARD2_0_COMPAT
        if (typeof(T) == typeof(Half)) { _dispatcher = (IDispatcher<T>)(object)new FloatingPointDispatcher<Half>(); return; }
#endif
#if !NETSTANDARD2_1_COMPAT
        try
        {
            if (typeof(IComparable<T>).IsAssignableFrom(typeof(T)))
            { _dispatcher = (IDispatcher<T>)Activator.CreateInstance(typeof(ComparableDispatcher<>).MakeGenericType(typeof(T)))!; return; }
        }
        catch { }
#else
        if (RuntimeFeature.IsDynamicCodeCompiled)
        {
#if NET7_0_OR_GREATER
            if (_isFloatingPointIeee)
            { _dispatcher = (IDispatcher<T>)Activator.CreateInstance(typeof(FloatingPointDispatcher<>).MakeGenericType(typeof(T)))!; return; }
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>() && typeof(T).GetInterface(typeof(INumber<>).FullName!) is not null)
            { _dispatcher = (IDispatcher<T>)Activator.CreateInstance(typeof(NumberDispatcher<>).MakeGenericType(typeof(T)))!; return; }
#endif
            if (typeof(IComparable<T>).IsAssignableFrom(typeof(T)))
            { _dispatcher = (IDispatcher<T>)Activator.CreateInstance(typeof(ComparableDispatcher<>).MakeGenericType(typeof(T)))!; return; }
        }
#endif
        _dispatcher = new DefaultDispatcher<T>();
    }
}

internal partial interface IDispatcher<T>;

internal sealed partial class FloatingPointDispatcher<T> : IDispatcher<T>
    where T : unmanaged,
#if NET7_0_OR_GREATER
        IFloatingPointIeee754<T>;
#else
        IComparable<T>;
#endif

internal sealed partial class NumberDispatcher<T> : IDispatcher<T>
    where T : unmanaged,
#if NET7_0_OR_GREATER
        INumber<T>;
#else
        IComparable<T>;
#endif

internal sealed partial class ComparableDispatcher<T> : IDispatcher<T>
    where T : IComparable<T>;

internal sealed partial class DefaultDispatcher<T> : IDispatcher<T>;
