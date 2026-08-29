
using System;
using System.Buffers.Binary;
using System.Net;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

#if NETSTANDARD2_0_COMPAT || NET
using System.Net.Sockets;
#endif

namespace SortSharp.Foundation;

#if NET7_0_OR_GREATER
internal readonly struct ComposedKeySelector<T, TInt, TKey, TLeft, TRight> : IKeySelector<T, TKey>
    where TLeft : IKeySelector<T, TInt>
    where TRight : IKeySelector<TInt, TKey>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TKey? Select(ref readonly T? item)
    {
        TInt? v = TLeft.Select(in item);
        return TRight.Select(in v);
    }
}
#else
internal readonly struct ComposedKeySelector<T, TInt, TKey, TLeft, TRight> : IKeySelector<T, TKey>
    where TLeft : unmanaged, IKeySelector<T, TInt>
    where TRight : unmanaged, IKeySelector<TInt, TKey>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TKey? SelectInst(ref readonly T? item)
    {
        TInt? v = default(TLeft).SelectInst(in item);
        return default(TRight).SelectInst(in v);
    }
}
#endif

internal readonly struct EnumUnderlyingSelector<TEnum, TUnderlying> : IKeySelector<TEnum, TUnderlying>
    where TEnum : struct, Enum
#if NET7_0_OR_GREATER
    where TUnderlying : struct, IBinaryInteger<TUnderlying>, IMinMaxValue<TUnderlying>
#else
    where TUnderlying : struct, IComparable<TUnderlying>
#endif
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TUnderlying Select(ref readonly TEnum item) => (TUnderlying)(object)item;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TUnderlying SelectInst(ref readonly TEnum item) => Select(in item);
}

internal readonly struct TimeSpanTicksSelector : IKeySelector<TimeSpan, long>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Select(ref readonly TimeSpan item) => item.Ticks;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long SelectInst(ref readonly TimeSpan item) => Select(in item);
}

internal readonly struct DateTimeTicksSelector : IKeySelector<DateTime, long>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Select(ref readonly DateTime item) => item.Ticks;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long SelectInst(ref readonly DateTime item) => Select(in item);
}

internal readonly struct DateTimeOffsetUtcTicksSelector : IKeySelector<DateTimeOffset, long>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Select(ref readonly DateTimeOffset item) => item.UtcTicks;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long SelectInst(ref readonly DateTimeOffset item) => Select(in item);
}

#if NETSTANDARD2_0_COMPAT
internal readonly struct DateOnlyDayNumberSelector : IKeySelector<DateOnly, int>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Select(ref readonly DateOnly item) => item.DayNumber;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int SelectInst(ref readonly DateOnly item) => Select(in item);
}

internal readonly struct TimeOnlyTicksSelector : IKeySelector<TimeOnly, long>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long Select(ref readonly TimeOnly item) => item.Ticks;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long SelectInst(ref readonly TimeOnly item) => Select(in item);
}
#endif

#if NETCOREAPP3_0_OR_GREATER
internal readonly struct RuneValueSelector : IKeySelector<Rune, int>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Select(ref readonly Rune item) => item.Value;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int SelectInst(ref readonly Rune item) => Select(in item);
}
#endif

#pragma warning disable CS0618
#if NETSTANDARD2_0_COMPAT || NET
internal readonly struct IPAddressV4Selector : IKeySelector<IPAddress, uint>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Select(ref readonly IPAddress? item)
        => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness((uint)item!.Address) : (uint)item!.Address;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint SelectInst(ref readonly IPAddress? item) => Select(in item);
}
#endif

#if NETSTANDARD2_1_COMPAT
internal readonly struct IPAddressV6Selector : IKeySelector<IPAddress, IPAddressV6Selector.Binary>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Binary Select(ref readonly IPAddress? item)
    {
        if (item is null) return default;
        else if (item.AddressFamily == AddressFamily.InterNetwork)
        {
            return new() { V4Mask = 0xFFFF, V4 = (uint)item.Address };
        }
        else
        {
            Binary bytes = new();
            if (!item.TryWriteBytes(MemoryMarshal.CreateSpan(ref Unsafe.As<Binary, byte>(ref bytes), 16), out _))
                ThrowHelper.ThrowInvalidOperation("Failed to write IPv6 bytes.");
            bytes.ScopeId = unchecked((ulong)IPAddress.HostToNetworkOrder(item.ScopeId + long.MinValue));
            return bytes;
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Binary SelectInst(ref readonly IPAddress? item) => Select(in item);

    [StructLayout(LayoutKind.Explicit, Pack = 8)]
    public struct Binary : IComparable<Binary>
    {
        [FieldOffset(0)] public ulong Higher;
        [FieldOffset(8)] public ulong Lower;
        [FieldOffset(10)] public ushort V4Mask;
        [FieldOffset(12)] public uint V4;
        [FieldOffset(16)] public ulong ScopeId;

        public readonly int CompareTo(Binary other)
        {
            if (Higher != other.Higher)
                return ToHostOrder(Higher).CompareTo(ToHostOrder(other.Higher));
            else if (Lower != other.Lower)
                return ToHostOrder(Lower).CompareTo(ToHostOrder(other.Lower));
            else
                return ToHostOrder(ScopeId).CompareTo(ToHostOrder(other.ScopeId));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static ulong ToHostOrder(ulong value)
                => BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(value) : value;
        }
    }
}
#pragma warning restore CS0618
#endif
