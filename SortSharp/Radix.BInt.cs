using System;
using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SortSharp.Compat;
using SortSharp.Foundation;
using SortSharp.SourceGenerators;
using static SortSharp.SpanOperations;

namespace SortSharp;

public static partial class Extensions
{
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixLsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="bitwidth"]/*' />
    [ApiTemplate("ulong")]
    public static void RadixLsdSort(this Span<ulong> span, int bitwidth = 8)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitwidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitwidth, 12);
        Radix.BInt<ulong>.LsdSort(span, bitwidth);
    }

    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixMsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="bitwidth"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="profile"]/*' />
    [ApiTemplate("ulong")]
    public static void RadixMsdSort(this Span<ulong> span, int bitwidth = 8, MemoryProfile profile = MemoryProfile.Baseline)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitwidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitwidth, 12);
        Radix.BInt<ulong>.MsdSort(span, bitwidth, profile);
    }

#if NET7_0_OR_GREATER
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixLsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/TypeParams/Member[@name="T"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="bitwidth"]/*' />
    [ApiTemplate(nameof(T), KeySelector = true)]
    public static void RadixLsdSort<T>(this Span<T> span, int bitwidth = 8)
        where T : unmanaged, IBinaryInteger<T>, IMinMaxValue<T>
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitwidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitwidth, 12);
        Radix.BInt<T>.LsdSort(span, bitwidth);
    }

    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixMsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/TypeParams/Member[@name="T"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="bitwidth"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="profile"]/*' />
    [ApiTemplate(nameof(T), KeySelector = true)]
    public static void RadixMsdSort<T>(this Span<T> span, int bitwidth = 8, MemoryProfile profile = MemoryProfile.Baseline)
        where T : unmanaged, IBinaryInteger<T>, IMinMaxValue<T>
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bitwidth, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bitwidth, 12);
        Radix.BInt<T>.MsdSort(span, bitwidth, profile);
    }

#endif
}

[Sort]
internal static partial class Radix
{
    internal static partial class BInt<T>
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

        [ImplTemplate(nameof(T), nameof(span))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void FallbackSort(Span<T> span)
        {
            PDQ.Op<T>.Sort(span);
        }

        [ImplTemplate(nameof(T), nameof(span))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void FallbackSortStable(Span<T> span, MemoryProfile profile)
        {
            if (span.Length < 32)
                IPN.Cmp<T>.SmallSort(span);
            else
                Wiki.Cmp<T>.Sort(span, profile);
        }

        [ImplTemplate(nameof(T), nameof(span), nameof(cache))]
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

        private static T GetMask(short bitwidth)
#if NET7_0_OR_GREATER
                => unchecked((T.One << bitwidth) - T.One);
#else
        {
            unchecked
            {
                if (typeof(T) == typeof(sbyte)) return (T)(object)(sbyte)((1 << bitwidth) - 1);
                else if (typeof(T) == typeof(byte)) return (T)(object)(byte)((1 << bitwidth) - 1);
                else if (typeof(T) == typeof(short)) return (T)(object)(short)((1 << bitwidth) - 1);
                else if (typeof(T) == typeof(ushort)) return (T)(object)(ushort)((1 << bitwidth) - 1);
                else if (typeof(T) == typeof(int)) return (T)(object)((1 << bitwidth) - 1);
                else if (typeof(T) == typeof(uint)) return (T)(object)((1U << bitwidth) - 1);
                else if (typeof(T) == typeof(long)) return (T)(object)((1L << bitwidth) - 1L);
                else if (typeof(T) == typeof(ulong)) return (T)(object)((1UL << bitwidth) - 1UL);
                else if (typeof(T) == typeof(nint)) return (T)(object)(nint)((1L << bitwidth) - 1L);
                else if (typeof(T) == typeof(nuint)) return (T)(object)(nuint)((1UL << bitwidth) - 1UL);
                ThrowHelper.ThrowUnreachable();
                return default; // unreachable
            }
        }
#endif

        [StructLayout(LayoutKind.Auto)]
        private ref struct State(short bitwidth, short offset) : IState
        {
            private short Step = bitwidth;
            public short Offset { get; private set; } = offset;
            /// <remarks>cached for performance, keep in sync with <see cref="Step"/>.</remarks>
            public T Mask { get; private set; } = GetMask(bitwidth);

            public static State CreateLsd(short bitwidth) => new(bitwidth, 0);
            public static State CreateMsd(short bitwidth) => new(bitwidth, (short)(Width - bitwidth));

            public readonly int BucketCount => 1 << Step;

            public bool TryMoveFromLsd()
            {
                Offset += Step;
                Step = (short)Math.Min(Step, Width - Offset);
                return Offset < Width;
            }
            public bool TryMoveFromMsd()
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
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Extract(ref readonly T value, ref readonly State state)
#if NET7_0_OR_GREATER
            => Convert.ToInt32<T>(((value ^ T.MinValue) >>> state.Offset) & state.Mask);
#else
            => value switch
            {
                sbyte i8 => ((i8 ^ sbyte.MinValue) >>> state.Offset) & (sbyte)(object)state.Mask,
                byte u8 => (u8 >>> state.Offset) & (byte)(object)state.Mask,
                short i16 => ((i16 ^ short.MinValue) >>> state.Offset) & (short)(object)state.Mask,
                ushort u16 => (u16 >>> state.Offset) & (ushort)(object)state.Mask,
                int i32 => ((i32 ^ int.MinValue) >>> state.Offset) & (int)(object)state.Mask,
                uint u32 => (int)((u32 >>> state.Offset) & (uint)(object)state.Mask),
                long i64 => (int)(((i64 ^ long.MinValue) >>> state.Offset) & (long)(object)state.Mask),
                ulong u64 => (int)((u64 >>> state.Offset) & (ulong)(object)state.Mask),
                nint isize => (int)(((isize ^ nint.MinValue) >>> state.Offset) & (nint)(object)state.Mask),
                nuint usize => (int)((usize >>> state.Offset) & (nuint)(object)state.Mask),
                _ => ThrowHelper.ThrowUnreachable()
            };
#endif

        /// <remarks>Stub for variable-length types.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool MsdSkipBucket(int _) => false;

        [ImplTemplate(nameof(T), Broadcast = nameof(Radix))]
        static void GetHistogram(ReadOnlySpan<T> span, Span<int> counts, ref readonly State state)
        {
            ref readonly T first = ref span.Ref(0);
            ref readonly T last = ref span.Ref(span.Length);
            for (; Unsafe.IsAddressLessThan(in first, in last); first = ref Unsafe.RO.Add(in first, 1))
                counts.Ref(Extract(in first, in state))++;
        }

        [ImplTemplate(nameof(T), nameof(src), nameof(dst), Broadcast = nameof(Radix))]
        static void CopyByHistogram(ReadOnlySpan<T> src, Span<T> dst, Span<int> heads, ref readonly State state)
        {
            for (int i = 0; i < src.Length; i++)
            {
                ref readonly T t = ref src.Ref(i);
                int b = Extract(in t, in state);
                int d = heads.Ref(b)++;
                Ensure(d < dst.Length); // manual bound check
                dst.Ref(d) = t;
            }
        }

        [ImplTemplate(nameof(T), nameof(src), nameof(dst), Broadcast = nameof(Radix))]
        [SkipLocalsInit]
        static void LsdSortLoop(Span<T> src, Span<T> dst, Span<int> heads, State state, bool parity = false)
        {
            Span<int> c = stackalloc int[state.BucketCount];
            while (true)
            {
                Span<int> counts = c.Sub(0, state.BucketCount);
                counts.Clear();
                GetHistogram(src, counts, in state);
                int b = FindSingleBucket(counts, src.Length);
                if (b >= 0)
                {
                    if (state.TryMoveFromLsd())
                        continue;
                    else if (parity)
                        src.CopyTo(dst);
                    return;
                }

                counts.CopyTo(heads.Sub(1, heads.Length));
                heads.Ref(0) = 0; // clearing
                PartialSums(heads);

                CopyByHistogram(src, dst, heads, in state);

                if (!state.TryMoveFromLsd())
                    break;

                Span<T> tmp = src;
                src = dst;
                dst = tmp;
                parity = !parity;
            }

            if (!parity)
                dst.CopyTo(src);
        }

        [ImplTemplate(nameof(T), nameof(span))]
        [SkipLocalsInit]
        internal static void LsdSort(Span<T> span, int bitwidth)
        {
            Debug.Assert(bitwidth > 0 && bitwidth <= Math.Min(16, Width));

            if (span.Length > FallbackSortThreshold)
            {
                var state = State.CreateLsd((short)bitwidth);
                using MemoryOwner<T> owner = new(MemoryPool<T>.Shared.Rent(span.Length));
                LsdSortLoop(span, owner.Memory.Span.Sub(0, span.Length), stackalloc int[state.BucketCount + 1], state);
            }
            else if (span.Length > 1)
                FallbackSortStable(span, MemoryProfile.High);
        }

        /// <seealso href="https://github.com/skarupke/radix_sort"/>
        [ImplTemplate(nameof(T), nameof(span), Broadcast = nameof(Radix))]
        [SkipLocalsInit]
        static void SkaSwap(Span<T> span, Span<int> counts, Span<int> heads, int bucketCount, ref readonly State state)
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
                        int tb = Extract(in t, in state);
                        int d = heads.Ref(tb)++;
                        Ensure(d < span.Length); // manual bound check
                        Swap(ref t, ref span.Ref(d));
                    }

                    prev = b;
                    b = next;
                }
            }
        }

        [ImplTemplate(nameof(T), Broadcast = nameof(Radix))]
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

        [ImplTemplate(nameof(T), nameof(src), nameof(dst), Broadcast = nameof(Radix))]
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void MsdSortBuffered(Span<T> src, Span<T> dst, Span<int> heads, State state, bool stable, bool parity = false)
        {
            Span<int> counts = stackalloc int[state.BucketCount];
        rewind:
            GetHistogram(src, counts, in state);
            int b = FindSingleBucket(counts, src.Length);
            if (b >= 0)
            {
                if (!MsdSkipBucket(b) && state.TryMoveFromMsd())
                {
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
            PartialSums(heads);

            CopyByHistogram(src, dst, heads, in state);

            if (!state.TryMoveFromMsd())
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

        /// <seealso href="https://github.com/lakwet/voracious_sort" />
        [ImplTemplate(nameof(T), nameof(span), Broadcast = nameof(Radix))]
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void MsdSortUnstable(Span<T> span, Span<int> heads, State state, MemoryProfile profile, int heuristic = 2)
        {
            if (profile >= MemoryProfile.Baseline && CanStackAlloc<T>(span.Length))
            {
                Span<T> cache = stackalloc T[span.Length]; // could skip locals init
                MsdSortBuffered(span, cache, heads, state, false);
                return;
            }

            Span<int> counts = stackalloc int[state.BucketCount];
        rewind:
            GetHistogram(span, counts, in state);
            int b = FindSingleBucket(counts, span.Length);
            if (b >= 0)
            {
                if (!MsdSkipBucket(b) && state.TryMoveFromMsd())
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
            PartialSums(heads);
            SkaSwap(span, counts, heads, state.BucketCount, in state);
            Span<int> tails = counts; // re-purposed. heads are now invalid.

            if (!state.TryMoveFromMsd())
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

        [ImplTemplate(nameof(T), nameof(span))]
        [SkipLocalsInit]
        internal static void MsdSort(Span<T> span, int bitwidth, MemoryProfile profile)
        {
            Debug.Assert(bitwidth > 0 && bitwidth <= Math.Min(16, Width));

            if (span.Length > FallbackSortThreshold)
            {
                var state = State.CreateMsd((short)Math.Min(bitwidth, Width));
                Span<int> sums = stackalloc int[state.BucketCount + 1];
                if (profile < MemoryProfile.High) // unstable
                    MsdSortUnstable(span, sums, state, profile);
                else
                {
                    using MemoryOwner<T> owner = new(MemoryPool<T>.Shared.Rent(span.Length));
                    MsdSortBuffered(span, owner.Memory.Span.Sub(0, span.Length), sums, state, true);
                }
            }
            else if (span.Length > 1)
                if (profile < MemoryProfile.High) // unstable
                    FallbackSort(span);
                else
                    FallbackSortStable(span, profile);
        }

        internal static partial class From<V, TSelector>
            where TSelector :
#if !NET7_0_OR_GREATER
                unmanaged,
#endif
                IKeySelector<V, T>
        {
            private static readonly KeySelectorComparer<V, T, TSelector> Comparer = new();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static int Compare(ref readonly V a, ref readonly V b) => Comparer.Compare(a, b);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static int Extract(ref readonly V value, ref readonly State state)
            {
#if NET7_0_OR_GREATER
                T key = TSelector.Select(in value);
#else
                T key = default(TSelector).SelectInst(in value);
#endif
                return BInt<T>.Extract(in key, in state);
            }
        }
    }
}
