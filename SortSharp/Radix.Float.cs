using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using SortSharp.Compat;
using SortSharp.Foundation;
using SortSharp.SourceGeneration;
using Digit = int;

namespace SortSharp;

public static partial class Extensions
{
    /// <remarks>IEEE 754 total order is implied, as if <see cref="TotalOrderIeee754Comparer{T}"/> is used.</remarks>
    /// <inheritdoc cref="RadixLsdSort(Span{ulong}, int)" />
    [SpecializationTemplate("double")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixLsdSort(this Span<double> span)
    {
        Radix.Float<double>.LsdSort(span);
    }

    /// <remarks>IEEE 754 total order is implied, as if <see cref="TotalOrderIeee754Comparer{T}"/> is used.</remarks>
    /// <inheritdoc cref="RadixMsdSort(Span{ulong}, int, MemoryProfile)" />
    [SpecializationTemplate("double")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixMsdSort(this Span<double> span, MemoryProfile profile = MemoryProfile.Baseline)
    {
        Radix.Float<double>.MsdSort(span, profile);
    }

    /// <remarks>IEEE 754 total order is implied, as if <see cref="TotalOrderIeee754Comparer{T}"/> is used.</remarks>
    /// <inheritdoc cref="RadixLsdSort{V}(Span{ulong}, Span{V}, int)" />
    [SpecializationTemplate("double")]
    public static void RadixLsdSort<V>(this Span<double> keys, Span<V> items)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(keys.Length, items.Length, nameof(items));
        Radix.Float<double>.LsdSort(keys, items);
    }

    /// <remarks>IEEE 754 total order is implied, as if <see cref="TotalOrderIeee754Comparer{T}"/> is used.</remarks>
    /// <inheritdoc cref="RadixMsdSort{V}(Span{ulong}, Span{V}, int, MemoryProfile)" />
    [SpecializationTemplate("double")]
    public static void RadixMsdSort<V>(this Span<double> keys, Span<V> items, MemoryProfile profile = MemoryProfile.Baseline)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(keys.Length, items.Length, nameof(items));
        Radix.Float<double>.MsdSort(keys, items, profile);
    }

#if NET7_0_OR_GREATER
    /// <remarks>IEEE 754 total order is implied, as if <see cref="TotalOrderIeee754Comparer{T}"/> is used.</remarks>
    /// <inheritdoc cref="RadixLsdSort{T}(Span{T}, int)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixLsdSort<T>(this Span<T> span)
        where T : unmanaged, IBinaryFloatingPointIeee754<T>, IMinMaxValue<T>
    {
        Radix.Float<T>.LsdSort(span);
    }

    /// <remarks>IEEE 754 total order is implied, as if <see cref="TotalOrderIeee754Comparer{T}"/> is used.</remarks>
    /// <inheritdoc cref="RadixMsdSort{T}(Span{T}, int, MemoryProfile)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixMsdSort<T>(this Span<T> span, MemoryProfile profile = MemoryProfile.Baseline)
        where T : unmanaged, IBinaryFloatingPointIeee754<T>, IMinMaxValue<T>
    {
        Radix.Float<T>.MsdSort(span, profile);
    }

    /// <remarks>IEEE 754 total order is implied, as if <see cref="TotalOrderIeee754Comparer{T}"/> is used.</remarks>
    /// <inheritdoc cref="RadixLsdSort{K, V}(Span{K}, Span{V}, int)" />
    public static void RadixLsdSort<T, V>(this Span<T> keys, Span<V> items)
        where T : unmanaged, IBinaryFloatingPointIeee754<T>, IMinMaxValue<T>
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(keys.Length, items.Length, nameof(items));
        Radix.Float<T>.LsdSort(keys, items);
    }

    /// <remarks>IEEE 754 total order is implied, as if <see cref="TotalOrderIeee754Comparer{T}"/> is used.</remarks>
    /// <inheritdoc cref="RadixMsdSort{K, V}(Span{K}, Span{V}, int, MemoryProfile)" />
    public static void RadixMsdSort<K, V>(this Span<K> keys, Span<V> items, MemoryProfile profile = MemoryProfile.Baseline)
        where K : unmanaged, IBinaryFloatingPointIeee754<K>, IMinMaxValue<K>
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(keys.Length, items.Length, nameof(items));
        Radix.Float<K>.MsdSort(keys, items, profile);
    }
#endif
}

internal static partial class Radix
{
    [SortSpecialization]
    internal static partial class Float<T>
        where T : unmanaged,
#if NET7_0_OR_GREATER
        IBinaryFloatingPointIeee754<T>, IMinMaxValue<T>
#else
        IComparable<T>
#endif
    {
        static readonly TotalOrderIeee754Comparer<T> Comparer = new();
        static readonly int Width;

        static Float()
        {
#if NET7_0_OR_GREATER
            int bits = 1 + T.MaxValue.GetSignificandBitLength() + T.PositiveInfinity.GetExponentShortestBitLength();
            Width = Math.DivRem(bits, 8, out int rem) switch
            {
                var w when w >= 1 => w,
                var w when w == 0 && rem > 0 => 1, // FP4?
                _ => 0, // exception
            };
#else
            Width = Unsafe.SizeOf<T>() * 8;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Compare(ref readonly T a, ref readonly T b) => Comparer.Compare(a, b);

        [OverloadTemplate(nameof(T), null, nameof(span))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void FallbackSort(Span<T> span)
        {
            PDQ.Cmp<T, TotalOrderIeee754Comparer<T>>.Sort(span, Comparer);
        }

        [OverloadTemplate(nameof(T), null, nameof(span))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void FallbackSortStable(Span<T> span, MemoryProfile profile)
        {
            if (span.Length < 32)
                IPN.Cmp<T, TotalOrderIeee754Comparer<T>>.SmallSort(span, Comparer);
            else
                Wiki.Cmp<T, TotalOrderIeee754Comparer<T>>.Sort(span, Comparer, profile);
        }

        [OverloadTemplate(nameof(T), null, nameof(span), nameof(cache))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void FallbackSortStableReuse(Span<T> span, Span<T> cache)
        {
            if (span.Length < 32)
                IPN.Cmp<T, TotalOrderIeee754Comparer<T>>.SmallSort(span, Comparer);
            else
            {
                var iterator = new Wiki.Iterator(span.Length, 4);
                Wiki.Cmp<T, TotalOrderIeee754Comparer<T>>.BlockSortLoop(span, Comparer, ref iterator);
                Wiki.Cmp<T, TotalOrderIeee754Comparer<T>>.SortLoop(span, cache, Comparer, ref iterator);
            }
        }

        private ref struct LsdState()
        {
            public Digit Offset { get; private set; } =
#if NET7_0_OR_GREATER
                BitConverter.IsLittleEndian ? 0 :
#endif
                Width - 1;

            public readonly int BucketCount => 1 << 8;

            public bool MoveNext()
            {
#if NET7_0_OR_GREATER
                if (BitConverter.IsLittleEndian)
#endif
                    return ++Offset < Width;
#if NET7_0_OR_GREATER
                else
                    return --Offset >= 0;
#endif
            }

            public readonly Digit Current => Offset;
        }

        private ref struct MsdState()
        {
            public Digit Offset { get; private set; } =
#if NET7_0_OR_GREATER
                BitConverter.IsLittleEndian ? Width - 1 : 0;
#else
                Width - 1;
#endif

            public readonly int BucketCount => 1 << 8;

            public bool MoveNext()
            {
#if NET7_0_OR_GREATER
                if (BitConverter.IsLittleEndian)
#endif
                    return --Offset >= 0;
#if NET7_0_OR_GREATER
                else
                    return ++Offset < Width;
#endif
            }

            public readonly Digit Current => Offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Extract(ref readonly T value, ref readonly Digit digit)
        {
#if NET7_0_OR_GREATER
            T v = T.IsNegative(value) ? ~value : -value;
            return Unsafe.Add(ref Unsafe.As<T, byte>(ref v), digit);
#else
            if (value is double d)
            {
                long i = BitConverter.DoubleToInt64Bits(d);
                return (int)(((i < 0 ? ~i : i ^ long.MinValue) >>> (digit * 8)) & 0xff);
            }
            else if (value is float f)
            {
                int i = BitConverter.SingleToInt32Bits(f);
                return ((i < 0 ? ~i : i ^ int.MinValue) >>> (digit * 8)) & 0xff;
            }
#if NETSTANDARD2_0_COMPAT
            else if (value is Half h)
            {
                short i = BitConverter.HalfToInt16Bits(h);
                return ((i < 0 ? ~i : i ^ short.MinValue) >>> (digit * 8)) & 0xff;
            }
#endif
            throw new NotSupportedException();
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool MsdSkipBucket(int _) => false;

        [OverloadTemplate(nameof(T), null, nameof(span))]
        [SkipLocalsInit]
        internal static void LsdSort(Span<T> span)
        {
            if (span.Length > FallbackSortThreshold)
            {
                var state = new LsdState();
                using MemoryOwner<T> owner = new(MemoryPool<T>.Shared.Rent(span.Length));
                LsdSortLoop(span, owner.Memory.Span.Sub(0, span.Length), stackalloc int[state.BucketCount + 1], state);
            }
            else if (span.Length > 1)
                FallbackSortStable(span, MemoryProfile.High);
        }

        [OverloadTemplate(nameof(T), null, nameof(span))]
        [SkipLocalsInit]
        internal static void MsdSort(Span<T> span, MemoryProfile profile)
        {
            if (span.Length > FallbackSortThreshold)
            {
                var state = new MsdState();
                if (profile < MemoryProfile.High) // unstable
                    MsdSortUnstable(span, stackalloc int[state.BucketCount + 1], state, profile);
                else
                {
                    using MemoryOwner<T> owner = new(MemoryPool<T>.Shared.Rent(span.Length));
                    MsdSortBuffered(span, owner.Memory.Span.Sub(0, span.Length), stackalloc int[state.BucketCount + 1], state, true);
                }
            }
            else if (span.Length > 1)
            {
                if (profile < MemoryProfile.High) // unstable
                    FallbackSort(span);
                else
                    FallbackSortStable(span, profile);
            }
        }
    }
}
