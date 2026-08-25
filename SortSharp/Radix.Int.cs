using System;
using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SortSharp.Compat;
using SortSharp.Foundation;
using SortSharp.SourceGeneration;
using static SortSharp.SpanOperations;

namespace SortSharp;

public static partial class Extensions
{
    /// <summary>
    /// Sorts the elements using the least significant digit (LSD) radix sort algorithm,
    /// which is <see href="https://en.wikipedia.org/wiki/Sorting_algorithm#Stability">stable</see>.
    /// </summary>
    /// <param name="span">The span to sort.</param>
    /// <param name="bitWidth">The number of bits to sort at each iteration.</param>
    [SpecializationTemplate("ulong")]
    public static void RadixLsdSort(this Span<ulong> span, int bitWidth = 8)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitWidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitWidth, 12);
        Radix.Int<ulong>.LsdSort(span, bitWidth);
    }

    /// <summary>
    /// Sorts the elements using the most significant digit (MSD) radix sort algorithm,
    /// which is <see href="https://en.wikipedia.org/wiki/Sorting_algorithm#Stability">stable</see> only if <paramref name="profile"/> is <see cref="MemoryProfile.High" /> or higher.
    /// </summary>
    /// <param name="span">The span to sort.</param>
    /// <param name="bitWidth">The number of bits to sort at each iteration.</param>
    /// <param name="profile">The profile to use for allocating temporary cache. A fixed limit is applied with the default profile.</param>
    [SpecializationTemplate("ulong")]
    public static void RadixMsdSort(this Span<ulong> span, int bitWidth = 8, MemoryProfile profile = MemoryProfile.Baseline)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitWidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitWidth, 12);
        Radix.Int<ulong>.MsdSort(span, bitWidth, profile);
    }

    /// <typeparam name="V">The type of the values.</typeparam>
    /// <param name="keys">The span of keys to sort.</param>
    /// <param name="items">The span of values to sort.</param>
    /// <param name="bitWidth">The number of bits to sort at each iteration.</param>
    /// <inheritdoc cref="RadixLsdSort(Span{ulong}, int)" />
    [SpecializationTemplate("ulong")]
    public static void RadixLsdSort<V>(this Span<ulong> keys, Span<V> items, int bitWidth = 8)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(keys.Length, items.Length, nameof(items));
        ArgumentOutOfRangeException.ThrowIfLessThan(bitWidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitWidth, 12);
        Radix.Int<ulong>.LsdSort(keys, items, bitWidth);
    }

    /// <typeparam name="V">The type of the values.</typeparam>
    /// <param name="keys">The span of keys to sort.</param>
    /// <param name="items">The span of values to sort.</param>
    /// <param name="bitWidth">The number of bits to sort at each iteration.</param>
    /// <param name="profile">The profile to use for allocating temporary cache. A fixed limit is applied with the default profile.</param>
    /// <inheritdoc cref="RadixMsdSort(Span{ulong}, int, MemoryProfile)" />
    [SpecializationTemplate("ulong")]
    public static void RadixMsdSort<V>(this Span<ulong> keys, Span<V> items, int bitWidth = 8, MemoryProfile profile = MemoryProfile.Baseline)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(keys.Length, items.Length, nameof(items));
        ArgumentOutOfRangeException.ThrowIfLessThan(bitWidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitWidth, 12);
        Radix.Int<ulong>.MsdSort(keys, items, bitWidth, profile);
    }

#if NET7_0_OR_GREATER
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <inheritdoc cref="RadixLsdSort(Span{ulong}, int)" />
    public static void RadixLsdSort<T>(this Span<T> span, int bitWidth = 8)
        where T : unmanaged, IBinaryInteger<T>, IMinMaxValue<T>
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitWidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitWidth, 12);
        Radix.Int<T>.LsdSort(span, bitWidth);
    }

    /// <typeparam name="T">The type of elements.</typeparam>
    /// <inheritdoc cref="RadixMsdSort(Span{ulong}, int, MemoryProfile)" />
    public static void RadixMsdSort<T>(this Span<T> span, int bitWidth = 8, MemoryProfile profile = MemoryProfile.Baseline)
        where T : unmanaged, IBinaryInteger<T>, IMinMaxValue<T>
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitWidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitWidth, 12);
        Radix.Int<T>.MsdSort(span, bitWidth, profile);
    }

    /// <typeparam name="K">The type of the keys.</typeparam>
    /// <typeparam name="V">The type of the values.</typeparam>
    /// <inheritdoc cref="RadixLsdSort{V}(Span{ulong}, Span{V}, int)" />
    public static void RadixLsdSort<K, V>(this Span<K> keys, Span<V> items, int bitWidth = 8)
        where K : unmanaged, IBinaryInteger<K>, IMinMaxValue<K>
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(keys.Length, items.Length, nameof(items));
        ArgumentOutOfRangeException.ThrowIfLessThan(bitWidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitWidth, 12);
        Radix.Int<K>.LsdSort(keys, items, bitWidth);
    }

    /// <typeparam name="K">The type of the keys.</typeparam>
    /// <typeparam name="V">The type of the values.</typeparam>
    /// <inheritdoc cref="RadixMsdSort{V}(Span{ulong}, Span{V}, int, MemoryProfile)" />
    public static void RadixMsdSort<K, V>(this Span<K> keys, Span<V> items, int bitWidth = 8, MemoryProfile profile = MemoryProfile.Baseline)
        where K : unmanaged, IBinaryInteger<K>, IMinMaxValue<K>
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(keys.Length, items.Length, nameof(items));
        ArgumentOutOfRangeException.ThrowIfLessThan(bitWidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitWidth, 12);
        Radix.Int<K>.MsdSort(keys, items, bitWidth, profile);
    }
#endif
}

[Sort(Properties = SortProperties.NonComparison)]
internal static partial class Radix
{
    const int MaxStackAllocSize = 1 << 16;
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
                throw new InvalidOperationException();
            }
        }
        for (; i < counts.Length; i++)
        {
            int c = counts.Ref(i);
            if (c > 0)
                return c == length ? i : -1;
        }
        throw new InvalidOperationException();
    }

    internal static partial class Int<T>
        where T : unmanaged,
#if NET7_0_OR_GREATER
        IBinaryInteger<T>, IMinMaxValue<T>
#else
        IComparable<T>
#endif
    {
        static readonly int Width =
#if NET7_0_OR_GREATER
            T.MaxValue.GetByteCount() * 8;
#else
            Unsafe.SizeOf<T>() * 8;
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Compare(ref readonly T a, ref readonly T b) => a.CompareTo(b);

        [OverloadTemplate(nameof(T), null, nameof(span))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void FallbackSort(Span<T> span)
        {
            PDQ.Op<T>.Sort(span);
        }

        [OverloadTemplate(nameof(T), null, nameof(span))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void FallbackSortStable(Span<T> span, MemoryProfile profile)
        {
            if (span.Length < 32)
                IPN.Cmp<T>.SmallSort(span);
            else
                Wiki.Cmp<T>.Sort(span, profile);
        }

        [OverloadTemplate(nameof(T), null, nameof(span), nameof(cache))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void FallbackSortStableReuse(Span<T> span, Span<T> cache)
        {
            if (span.Length < 32)
                IPN.Cmp<T>.SmallSort(span);
            else
            {
                var iterator = new Wiki.Iterator(span.Length, 4);
                Wiki.Cmp<T>.BlockSortLoop(span, ref iterator);
                Wiki.Cmp<T>.SortLoop(span, cache, ref iterator);
            }
        }

        private static T GetMask(short bitWidth)
#if NET7_0_OR_GREATER
                => unchecked((T.One << bitWidth) - T.One);
#else
        {
            unchecked
            {
                if (typeof(T) == typeof(sbyte)) return (T)(object)(sbyte)((1 << bitWidth) - 1);
                else if (typeof(T) == typeof(byte)) return (T)(object)(byte)((1 << bitWidth) - 1);
                else if (typeof(T) == typeof(short)) return (T)(object)(short)((1 << bitWidth) - 1);
                else if (typeof(T) == typeof(ushort)) return (T)(object)(ushort)((1 << bitWidth) - 1);
                else if (typeof(T) == typeof(int)) return (T)(object)((1 << bitWidth) - 1);
                else if (typeof(T) == typeof(uint)) return (T)(object)((1U << bitWidth) - 1);
                else if (typeof(T) == typeof(long)) return (T)(object)((1L << bitWidth) - 1L);
                else if (typeof(T) == typeof(ulong)) return (T)(object)((1UL << bitWidth) - 1UL);
                else if (typeof(T) == typeof(nint)) return (T)(object)(nint)((1L << bitWidth) - 1L);
                else if (typeof(T) == typeof(nuint)) return (T)(object)(nuint)((1UL << bitWidth) - 1UL);
                else throw new NotSupportedException();
            }
        }
#endif

        [StructLayout(LayoutKind.Auto)]
        private readonly struct Digit(short offset, T mask)
        {
            public readonly T Mask = mask;
            public readonly short Offset = offset;
        }
        
        [StructLayout(LayoutKind.Auto)]
        private ref struct LsdState(short bitWidth)
        {
            public short Step { get; private set; } = bitWidth;
            public short Offset { get; private set; } = 0;
            /// <remarks>cached for performance.</remarks>
            public readonly T Mask { get; } = GetMask(bitWidth); 

            public readonly int BucketCount => 1 << Step;

            public bool MoveNext()
            {
                Offset += Step;
                Step = (short)Math.Min(Step, Width - Offset);
                return Offset < Width;
            }

            public readonly Digit Current => new(Offset, Mask);
        }

        [StructLayout(LayoutKind.Auto)]
        private ref struct MsdState(short bitWidth)
        {
            public short Step { get; private set; } = bitWidth;
            public short Offset { get; private set; } = (short)(Width - bitWidth);
            /// <remarks>cached for performance, keep in sync with <see cref="Step"/>.</remarks>
            public T Mask { get; private set; } = GetMask(bitWidth); 

            public readonly int BucketCount => 1 << Step;

            public bool MoveNext()
            {
                if (Offset >= Step)
                {
                    Offset -= Step;
                    return true;
                }
                else if (Offset > 0)
                {
                    Step = Offset;
                    Offset = 0;
                    Mask = GetMask(Step);
                    return true;
                }
                else
                    return false;
            }

            public readonly Digit Current => new(Offset, Mask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Extract(ref readonly T value, ref readonly Digit digit)
#if NET7_0_OR_GREATER
            => Convert.ToInt32<T>(((value ^ T.MinValue) >>> digit.Offset) & digit.Mask);
#else
            => value switch
            {
                sbyte i8 => ((i8 ^ sbyte.MinValue) >>> digit.Offset) & (sbyte)(object)digit.Mask,
                byte u8 => (u8 >>> digit.Offset) & (byte)(object)digit.Mask,
                short i16 => ((i16 ^ short.MinValue) >>> digit.Offset) & (short)(object)digit.Mask,
                ushort u16 => (u16 >>> digit.Offset) & (ushort)(object)digit.Mask,
                int i32 => ((i32 ^ int.MinValue) >>> digit.Offset) & (int)(object)digit.Mask,
                uint u32 => (int)((u32 >>> digit.Offset) & (uint)(object)digit.Mask),
                long i64 => (int)(((i64 ^ long.MinValue) >>> digit.Offset) & (long)(object)digit.Mask),
                ulong u64 => (int)((u64 >>> digit.Offset) & (ulong)(object)digit.Mask),
                nint isize => (int)(((isize ^ nint.MinValue) >>> digit.Offset) & (nint)(object)digit.Mask),
                nuint usize => (int)((usize >>> digit.Offset) & (nuint)(object)digit.Mask),
                _ => throw new NotSupportedException()
            };
#endif

        /// <remarks>Stub for variable-length types.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool MsdSkipBucket(int _) => false;

        [OverloadTemplate(nameof(T), null, Enable = OptionalOverloads.SiblingSpecializations)]
        static void GetHistogram(ReadOnlySpan<T> span, Span<int> counts, ref readonly Digit digit)
        {
            ref readonly T first = ref span.Ref(0);
            ref readonly T last = ref span.Ref(span.Length);
            for (; Unsafe.IsAddressLessThan(in first, in last); first = ref Unsafe.RO.Add(in first, 1))
                counts.Ref(Extract(in first, in digit))++;
        }

        [OverloadTemplate(nameof(T), null, nameof(src), nameof(dst), Enable = OptionalOverloads.SiblingSpecializations)]
        static void CopyByHistogram(ReadOnlySpan<T> src, Span<T> dst, Span<int> heads, ref readonly Digit digit)
        {
            for (int i = 0; i < src.Length; i++)
            {
                ref readonly T t = ref src.Ref(i);
                int b = Extract(in t, in digit);
                int d = heads.Ref(b)++;
                Ensure(d < dst.Length); // manual bound check
                dst.Ref(d) = t;
            }
        }

        [OverloadTemplate(nameof(T), null, nameof(src), nameof(dst), Enable = OptionalOverloads.SiblingSpecializations)]
        [SkipLocalsInit]
        static void LsdSortLoop(Span<T> src, Span<T> dst, Span<int> heads, LsdState state, bool parity = false)
        {
            Span<int> c = stackalloc int[state.BucketCount];
            while (true)
            {
                Digit digit = state.Current;
                Span<int> counts = c.Sub(0, state.BucketCount);
                counts.Clear();
                GetHistogram(src, counts, in digit);
                int b = FindSingleBucket(counts, src.Length);
                if (b >= 0)
                {
                    if (state.MoveNext())
                        continue;
                    else if (parity)
                        src.CopyTo(dst);
                    return;
                }

                counts.CopyTo(heads.Sub(1, heads.Length));
                heads.Ref(0) = 0; // clearing
                PrefixSums(heads);

                CopyByHistogram(src, dst, heads, in digit);

                if (!state.MoveNext())
                    break;

                Span<T> tmp = src;
                src = dst;
                dst = tmp;
                parity = !parity;
            }

            if (!parity)
                dst.CopyTo(src);
        }

        [OverloadTemplate(nameof(T), null, nameof(span))]
        [SkipLocalsInit]
        internal static void LsdSort(Span<T> span, int bitWidth)
        {
            Debug.Assert(bitWidth > 0 && bitWidth <= Math.Min(16, Width));

            if (span.Length > FallbackSortThreshold)
            {
                var state = new LsdState((short)bitWidth);
                using MemoryOwner<T> owner = new(MemoryPool<T>.Shared.Rent(span.Length));
                LsdSortLoop(span, owner.Memory.Span.Sub(0, span.Length), stackalloc int[state.BucketCount + 1], state);
            }
            else if (span.Length > 1)
                FallbackSortStable(span, MemoryProfile.High);
        }

        /// <remarks><see href="https://github.com/skarupke/radix_sort"/></remarks>
        [OverloadTemplate(nameof(T), null, nameof(span), Enable = OptionalOverloads.SiblingSpecializations)]
        [SkipLocalsInit]
        static void SkaSwap(Span<T> span, Span<int> counts, Span<int> heads, int bucketCount, ref readonly Digit digit)
        {
            Span<int> links = stackalloc int[bucketCount];
            for (int b = 0; b < links.Length; b += 1)
            {
                links.Ref(b) = b;
            }
            SortCounts(counts, links);
            int low = ComparisonOperations.Op<int>.LowerBound(counts, 1);

            // index to inline linked list
            int head = -1;
            for (int i = low; i < links.Length - 1; i++) // skip the largest group
            {
                int b = links.Ref(i);
                counts.Ref(b) = head;
                head = b;
            }

            // reused
            counts.CopyTo(links);
            Span<int> tails = counts;
            heads.Sub(1, tails.Length + 1).CopyTo(tails);

            while (head != -1)
            {
                int prev = -1;
                int b = head;

                while (b != -1)
                {
                    int next = links.Ref(b);
                    int offset = heads.Ref(b);
                    int len = tails.Ref(b) - offset;

                    Debug.Assert(len >= 0);

                    if (len == 0)
                    {
                        if (prev == -1)
                            head = next;
                        else
                            links.Ref(prev) = next;

                        b = next;
                        continue;
                    }

                    for (int o = 0; o < len; o++)
                    {
                        ref T t = ref span.Ref(offset + o);
                        int tb = Extract(in t, in digit);
                        int d = heads.Ref(tb)++;
                        Ensure(d < span.Length); // manual bound check
                        Swap(ref t, ref span.Ref(d));
                    }

                    prev = b;
                    b = next;
                }
            }
        }

        [OverloadTemplate(nameof(T), null, Enable = OptionalOverloads.SiblingSpecializations)]
        static bool TestSorted(ReadOnlySpan<T> span, out int cmp)
        {
            Debug.Assert(span.Length >= 3);
            cmp = 0;
            ref readonly T last = ref span.Ref(span.Length - 1);
            for (ref readonly T left = ref span.Ref(0); Unsafe.IsAddressLessThan(in left, in last);)
            {
                ref readonly T right = ref Unsafe.RO.Inc(in left);
                int c = Compare(in left, in right);
                if (cmp != 0 && c != 0 && (cmp ^ c) < 0)
                    return false;
                left = ref right;
                cmp |= c;
            }
            return true;
        }

        [OverloadTemplate(nameof(T), null, nameof(src), nameof(dst), Enable = OptionalOverloads.SiblingSpecializations)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void MsdSortBuffered(Span<T> src, Span<T> dst, Span<int> heads, MsdState state, bool stable, bool parity = false)
        {
            Digit digit = state.Current;
            Span<int> counts = stackalloc int[state.BucketCount];
        rewind:
            GetHistogram(src, counts, in digit);
            int b = FindSingleBucket(counts, src.Length);
            if (b >= 0)
            {
                if (!MsdSkipBucket(b) && state.MoveNext())
                {
                    digit = state.Current;
                    counts = counts.Sub(0, state.BucketCount);
                    counts.Clear();
                    goto rewind;
                }
                else if (parity)
                    src.CopyTo(dst);
                return;
            }

            counts.CopyTo(heads.Sub(1, heads.Length));
            heads.Ref(0) = 0; // clearing
            PrefixSums(heads);

            CopyByHistogram(src, dst, heads, in digit);

            if (!state.MoveNext())
            {
                if (!parity)
                    dst.CopyTo(src);
                return;
            }

            int head = 0;
            for (b = 0; b < counts.Length; b++)
            {
                int len = counts.Ref(b);
                Span<T> sub_src = src.Sub(head, head + len);
                Span<T> sub_dst = dst.Sub(head, head + len);
                head += len;
                if (MsdSkipBucket(b))
                {
                    if (!parity)
                        sub_dst.CopyTo(sub_src);
                }
                else if (sub_dst.Length >= FallbackSortThreshold)
                    MsdSortBuffered(sub_dst, sub_src, heads, state, stable, !parity); // ping-pong
                else
                {
                    if (sub_dst.Length > 1)
                    {
                        if (stable)
                            FallbackSortStableReuse(sub_dst, sub_src);
                        else
                            FallbackSort(sub_dst);
                    }
                    if (!parity)
                        sub_dst.CopyTo(sub_src);
                }
            }
        }

        /// <remarks><see href="https://github.com/lakwet/voracious_sort" /></remarks>
        [OverloadTemplate(nameof(T), null, nameof(span), Enable = OptionalOverloads.SiblingSpecializations)]
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void MsdSortUnstable(Span<T> span, Span<int> heads, MsdState state, MemoryProfile profile, int heuristic = 2)
        {
            if (profile >= MemoryProfile.Baseline && (nint)Unsafe.SizeOf<T>() * span.Length <= MaxStackAllocSize)
            {
                Span<T> cache = stackalloc T[span.Length]; // could skip locals init
                MsdSortBuffered(span, cache, heads, state, false);
                return;
            }

            Span<int> counts = stackalloc int[state.BucketCount];
        rewind:
            Digit digit = state.Current;
            GetHistogram(span, counts, in digit);
            int b = FindSingleBucket(counts, span.Length);
            if (b >= 0)
            {
                if (!MsdSkipBucket(b) && state.MoveNext())
                {
                    counts = counts.Sub(0, state.BucketCount);
                    counts.Clear();
                    goto rewind;
                }
                else
                    return;
            }

            counts.CopyTo(heads.Sub(1, heads.Length));
            heads.Ref(0) = 0; // clearing
            PrefixSums(heads);
            SkaSwap(span, counts, heads, state.BucketCount, in digit);
            Span<int> tails = counts; // re-purposed. heads are now invalid.

            if (!state.MoveNext())
                return;

            int head = 0;
            for (b = 0; b < tails.Length; b++)
            {
                int tail = tails.Ref(b);
                Span<T> subspan = span.Sub(head, tail);
                head = tail;
                if (MsdSkipBucket(b))
                    continue;
                else if (subspan.Length >= FallbackSortThreshold)
                {
                    if (heuristic > 0 && TestSorted(subspan, out int cmp))
                    {
                        if (cmp > 0)
                            subspan.Reverse();
                        continue;
                    }
                    MsdSortUnstable(subspan, heads, state, profile, heuristic - 1);
                }
                else if (subspan.Length > 1)
                    FallbackSort(subspan);
            }
        }

        [OverloadTemplate(nameof(T), null, nameof(span))]
        [SkipLocalsInit]
        internal static void MsdSort(Span<T> span, int bitWidth, MemoryProfile profile)
        {
            Debug.Assert(bitWidth > 0 && bitWidth <= Math.Min(16, Width));

            if (span.Length > FallbackSortThreshold)
            {
                var state = new MsdState((short)Math.Min(bitWidth, Width));
                if (profile < MemoryProfile.High) // unstable
                    MsdSortUnstable(span, stackalloc int[state.BucketCount + 1], state, profile);
                else
                {
                    using MemoryOwner<T> owner = new(MemoryPool<T>.Shared.Rent(span.Length));
                    MsdSortBuffered(span, owner.Memory.Span.Sub(0, span.Length), stackalloc int[state.BucketCount + 1], state, true);
                }
            }
            else if (span.Length > 1)
                if (profile < MemoryProfile.High) // unstable
                    FallbackSort(span);
                else
                    FallbackSortStable(span, profile);
        }
    }
}
