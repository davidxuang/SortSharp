using System;
using System.Net;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using SortSharp.Foundation;
using SortSharp.SourceGenerators;

namespace SortSharp;

/// <summary>
/// Provides extension methods for <see cref="Span{T}"/> of <see cref="Enum"/>.
/// </summary>
public static partial class EnumExtensions
{
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixLsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="bitwidth"]/*' />
    [ApiTemplate(nameof(T))]
    public static void RadixLsdSort<T>(this Span<T> span, int bitwidth = 8)
        where T : struct, Enum
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitwidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitwidth, 12);
        switch (Enum.GetUnderlyingType(typeof(T)).Name)
        {
            case nameof(SByte): Radix.BInt<sbyte>.From<T, EnumUnderlyingSelector<T, sbyte>>.LsdSort(span, bitwidth); break;
            case nameof(Byte): Radix.BInt<byte>.From<T, EnumUnderlyingSelector<T, byte>>.LsdSort(span, bitwidth); break;
            case nameof(Int16): Radix.BInt<short>.From<T, EnumUnderlyingSelector<T, short>>.LsdSort(span, bitwidth); break;
            case nameof(UInt16): Radix.BInt<ushort>.From<T, EnumUnderlyingSelector<T, ushort>>.LsdSort(span, bitwidth); break;
            case nameof(Int32): Radix.BInt<int>.From<T, EnumUnderlyingSelector<T, int>>.LsdSort(span, bitwidth); break;
            case nameof(UInt32): Radix.BInt<uint>.From<T, EnumUnderlyingSelector<T, uint>>.LsdSort(span, bitwidth); break;
            case nameof(Int64): Radix.BInt<long>.From<T, EnumUnderlyingSelector<T, long>>.LsdSort(span, bitwidth); break;
            case nameof(UInt64): Radix.BInt<ulong>.From<T, EnumUnderlyingSelector<T, ulong>>.LsdSort(span, bitwidth); break;
            default: ThrowHelper.ThrowUnreachable(); break;
        }
    }

    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixLsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="bitwidth"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="profile"]/*' />
    [ApiTemplate(nameof(T))]
    public static void RadixMsdSort<T>(this Span<T> span, int bitwidth = 8, MemoryProfile profile = MemoryProfile.Baseline)
        where T : struct, Enum
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitwidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitwidth, 12);
        switch (Enum.GetUnderlyingType(typeof(T)).Name)
        {
            case nameof(SByte): Radix.BInt<sbyte>.From<T, EnumUnderlyingSelector<T, sbyte>>.MsdSort(span, bitwidth, profile); break;
            case nameof(Byte): Radix.BInt<byte>.From<T, EnumUnderlyingSelector<T, byte>>.MsdSort(span, bitwidth, profile); break;
            case nameof(Int16): Radix.BInt<short>.From<T, EnumUnderlyingSelector<T, short>>.MsdSort(span, bitwidth, profile); break;
            case nameof(UInt16): Radix.BInt<ushort>.From<T, EnumUnderlyingSelector<T, ushort>>.MsdSort(span, bitwidth, profile); break;
            case nameof(Int32): Radix.BInt<int>.From<T, EnumUnderlyingSelector<T, int>>.MsdSort(span, bitwidth, profile); break;
            case nameof(UInt32): Radix.BInt<uint>.From<T, EnumUnderlyingSelector<T, uint>>.MsdSort(span, bitwidth, profile); break;
            case nameof(Int64): Radix.BInt<long>.From<T, EnumUnderlyingSelector<T, long>>.MsdSort(span, bitwidth, profile); break;
            case nameof(UInt64): Radix.BInt<ulong>.From<T, EnumUnderlyingSelector<T, ulong>>.MsdSort(span, bitwidth, profile); break;
            default: ThrowHelper.ThrowUnreachable(); break;
        }
    }
}

/// <summary>
/// Provides extension methods for <see cref="Span{T}"/> of <see cref="TimeSpan"/>.
/// </summary>
public static partial class TimeSpanExtensions
{
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixLsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="bitwidth"]/*' />
    [ApiTemplate(nameof(TimeSpan))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixLsdSort(this Span<TimeSpan> span, int bitwidth = 8)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitwidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitwidth, 12);
        Radix.BInt<long>.From<TimeSpan, TimeSpanTicksSelector>.LsdSort(span, bitwidth);
    }
    
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixMsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="bitwidth"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="profile"]/*' />
    [ApiTemplate(nameof(TimeSpan))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixMsdSort(this Span<TimeSpan> span, int bitwidth = 8, MemoryProfile profile = MemoryProfile.Baseline)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitwidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitwidth, 12);
        Radix.BInt<long>.From<TimeSpan, TimeSpanTicksSelector>.MsdSort(span, bitwidth, profile);
    }
}

/// <summary>
/// Provides extension methods for <see cref="Span{T}"/> of <see cref="DateTimeExtensions"/>.
/// </summary>
public static partial class DateTimeExtensions
{
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixLsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="bitwidth"]/*' />
    [ApiTemplate(nameof(DateTime))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixLsdSort(this Span<DateTime> span, int bitwidth = 8)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitwidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitwidth, 12);
        Radix.BInt<long>.From<DateTime, DateTimeTicksSelector>.LsdSort(span, bitwidth);
    }
    
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixMsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="bitwidth"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="profile"]/*' />
    [ApiTemplate(nameof(DateTime))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixMsdSort(this Span<DateTime> span, int bitwidth = 8, MemoryProfile profile = MemoryProfile.Baseline)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitwidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitwidth, 12);
        Radix.BInt<long>.From<DateTime, DateTimeTicksSelector>.MsdSort(span, bitwidth, profile);
    }
}

/// <summary>
/// Provides extension methods for <see cref="Span{T}"/> of <see cref="DateTimeOffset"/>.
/// </summary>
public static partial class DateTimeOffsetExtensions
{
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixLsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="bitwidth"]/*' />
    [ApiTemplate(nameof(DateTimeOffset))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixLsdSort(this Span<DateTimeOffset> span, int bitwidth = 8)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitwidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitwidth, 12);
        Radix.BInt<long>.From<DateTimeOffset, DateTimeOffsetUtcTicksSelector>.LsdSort(span, bitwidth);
    }
    
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixMsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="bitwidth"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="profile"]/*' />
    [ApiTemplate(nameof(DateTimeOffset))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixMsdSort(this Span<DateTimeOffset> span, int bitwidth = 8, MemoryProfile profile = MemoryProfile.Baseline)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitwidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitwidth, 12);
        Radix.BInt<long>.From<DateTimeOffset, DateTimeOffsetUtcTicksSelector>.MsdSort(span, bitwidth, profile);
    }
}

#if NETSTANDARD2_0_COMPAT
/// <summary>
/// Provides extension methods for <see cref="Span{T}"/> of <see cref="DateOnly"/>.
/// </summary>
public static partial class DateOnlyExtensions
{
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixLsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="bitwidth"]/*' />
    [ApiTemplate(nameof(DateOnly))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixLsdSort(this Span<DateOnly> span, int bitwidth = 8)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitwidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitwidth, 12);
        Radix.BInt<int>.From<DateOnly, DateOnlyDayNumberSelector>.LsdSort(span, bitwidth);
    }
    
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixMsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="bitwidth"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="profile"]/*' />
    [ApiTemplate(nameof(DateOnly))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixMsdSort(this Span<DateOnly> span, int bitwidth = 8, MemoryProfile profile = MemoryProfile.Baseline)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitwidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitwidth, 12);
        Radix.BInt<int>.From<DateOnly, DateOnlyDayNumberSelector>.MsdSort(span, bitwidth, profile);
    }
}

/// <summary>
/// Provides extension methods for <see cref="Span{T}"/> of <see cref="TimeOnly"/>.
/// </summary>
public static partial class TimeOnlyExtensions
{
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixLsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="bitwidth"]/*' />
    [ApiTemplate(nameof(TimeOnly))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixLsdSort(this Span<TimeOnly> span, int bitwidth = 8)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitwidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitwidth, 12);
        Radix.BInt<long>.From<TimeOnly, TimeOnlyTicksSelector>.LsdSort(span, bitwidth);
    }
    
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixMsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="bitwidth"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="profile"]/*' />
    [ApiTemplate(nameof(TimeOnly))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixMsdSort(this Span<TimeOnly> span, int bitwidth = 8, MemoryProfile profile = MemoryProfile.Baseline)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitwidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitwidth, 12);
        Radix.BInt<long>.From<TimeOnly, TimeOnlyTicksSelector>.MsdSort(span, bitwidth, profile);
    }
}
#endif

#if NETCOREAPP3_0_OR_GREATER
/// <summary>
/// Provides extension methods for <see cref="Span{T}"/> of <see cref="Rune"/>.
/// </summary>
public static partial class RuneExtensions
{
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixLsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="bitwidth"]/*' />
    [ApiTemplate(nameof(Rune))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixLsdSort(this Span<Rune> span, int bitwidth = 8)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitwidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitwidth, 12);
        Radix.BInt<int>.From<Rune, RuneValueSelector>.LsdSort(span, bitwidth);
    }
    
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixMsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="bitwidth"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="profile"]/*' />
    [ApiTemplate(nameof(Rune))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixMsdSort(this Span<Rune> span, int bitwidth = 8, MemoryProfile profile = MemoryProfile.Baseline)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitwidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitwidth, 12);
        Radix.BInt<int>.From<Rune, RuneValueSelector>.MsdSort(span, bitwidth, profile);
    }
}
#endif

#if NETSTANDARD2_0_COMPAT || NET
/// <summary>
/// Provides extension methods for <see cref="Span{T}"/> of <see cref="IPAddress"/>.
/// </summary>
public static partial class IPAddressExtensions
{
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixLsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="IPAddressV4"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="bitwidth"]/*' />
    [ApiTemplate(nameof(IPAddress))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixLsdSortV4(this Span<IPAddress> span, int bitwidth = 8)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitwidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitwidth, 12);
        Radix.BInt<uint>.From<IPAddress, IPAddressV4Selector>.LsdSort(span, bitwidth);
    }

    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixMsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="IPAddressV4"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="bitwidth"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="profile"]/*' />
    [ApiTemplate(nameof(IPAddress))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixMsdSortV4(this Span<IPAddress> span, int bitwidth = 8, MemoryProfile profile = MemoryProfile.Baseline)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitwidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitwidth, 12);
        Radix.BInt<uint>.From<IPAddress, IPAddressV4Selector>.MsdSort(span, bitwidth, profile);
    }

#if NETSTANDARD2_1_COMPAT
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixLsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="IPAddressV6"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    [ApiTemplate(nameof(IPAddress))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixLsdSortV6(this Span<IPAddress> span)
    {
        Radix.Bin<IPAddressV6Selector.Binary>.From<IPAddress, IPAddressV6Selector>.LsdSort(span, ByteOrder.BigEndian);
    }

    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixMsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="IPAddressV6"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="profile"]/*' />
    [ApiTemplate(nameof(IPAddress))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixMsdSortV6(this Span<IPAddress> span, MemoryProfile profile = MemoryProfile.Baseline)
    {
        Radix.Bin<IPAddressV6Selector.Binary>.From<IPAddress, IPAddressV6Selector>.MsdSort(span, ByteOrder.BigEndian, profile);
    }
#endif
}
#endif

internal static partial class Radix
{
    const int FallbackSortThreshold = 128;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SortCounts(Span<int> a, Span<int> b) => PDQ.Op<int>.Sort(a, b);

    static int FindSingleBucket(ReadOnlySpan<int> counts, int length)
    {
        int i = 0;
        if (Vector.IsHardwareAccelerated) // assumes Vector<int>.IsSupported
        {
            int width = Vector<int>.Count;
            int end = counts.Length - counts.Length % width;
            Vector<int> Z = Vector<int>.Zero;
            for (; i < end; i += width)
            {
                Vector<int> V = Unsafe.ReadUnaligned<Vector<int>>(ref Unsafe.As<int, byte>(ref Unsafe.AsRef(in counts.Ref(i))));
                if (Vector.EqualsAll(V, Z)) continue;
                for (end = i + width; i < end; i++)
                {
                    int c = counts.Ref(i);
                    if (c > 0)
                        return c == length ? i : -1;
                }
                ThrowHelper.ThrowUnreachable();
            }
        }
        for (; i < counts.Length; i++)
        {
            int c = counts.Ref(i);
            if (c > 0)
                return c == length ? i : -1;
        }
        ThrowHelper.ThrowUnreachable();
        return -1; // unreachable
    }

    private interface IState
    {
        int BucketCount { get; }
        bool TryMoveFromLsd();
        bool TryMoveFromMsd();
    }
}
