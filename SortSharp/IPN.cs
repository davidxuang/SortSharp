using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using SortSharp.Extensions;
using SortSharp.SourceGeneration;
using static SortSharp.Extensions.SpanExtensions;

namespace SortSharp;

public static partial class SpanExtensions
{
#if NET7_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IPNSort<T>(this Span<T> span)
        where T : unmanaged, INumber<T>
    {
        if (span.Length <= 1)
            return;
        else if (Router<T>.IsFloatingPointIeee)
            Router<T>.To.IPNSort(span);
        else
            IPN.Op<T>.Sort(span);
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IPNSort<T>(this Span<T> span, IComparer<T>? comparer = null)
        => IPNSort<T, IComparer<T>?>(span, comparer);

    public static void IPNSort<T>(this Span<T> span, Comparison<T> compare)
    {
        ArgumentNullException.ThrowIfNull(compare, nameof(compare));
        if (span.Length <= 1)
            return;

        IPN.Fn<T>.Sort(span, compare);
    }

    public static void IPNSort<T, TComparer>(this Span<T> span, TComparer comparer)
        where TComparer : IComparer<T>?
    {
        if (span.Length <= 1)
            return;
        else if (typeof(TComparer).IsValueType)
#pragma warning disable CS8631
            IPN.Cmp<T, TComparer>.Sort(span, comparer);
#pragma warning restore CS8631
        else if (comparer is null || comparer as IComparer<T> == Comparer<T>.Default)
            Router<T>.To.IPNSort(span);
        else
            IPN.Cmp<T, IComparer<T>>.Sort(span, comparer);
    }

#if NET7_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IPNSort<K, V>(this Span<K> keys, Span<V> items)
        where K : unmanaged, INumber<K>
    {
        ArgumentOutOfRangeException.ThrowIf(keys.Length != items.Length, nameof(items));

        if (keys.Length <= 1)
            return;
        else if (Router<K>.IsFloatingPointIeee)
            Router<K>.To.IPNSort(keys, items);
        else
            IPN.Op<K>.Sort(keys, items);
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IPNSort<K, V>(this Span<K> keys, Span<V> items, IComparer<K>? comparer = null)
        => IPNSort<K, V, IComparer<K>?>(keys, items, comparer);

    public static void IPNSort<K, V>(this Span<K> keys, Span<V> items, Comparison<K> compare)
    {
        ArgumentNullException.ThrowIfNull(compare, nameof(compare));
        ArgumentOutOfRangeException.ThrowIf(keys.Length != items.Length, nameof(items));

        if (keys.Length <= 1)
            return;
        IPN.Fn<K>.Sort(keys, items, compare);
    }

    public static void IPNSort<K, V, TComparer>(this Span<K> keys, Span<V> items, TComparer comparer)
        where TComparer : IComparer<K>?
    {
        ArgumentOutOfRangeException.ThrowIf(keys.Length != items.Length, nameof(items));

        if (keys.Length <= 1)
            return;
        else if (typeof(TComparer).IsValueType)
#pragma warning disable CS8631
            IPN.Cmp<K, TComparer>.Sort(keys, items, comparer);
#pragma warning restore CS8631
        else if (comparer is null || comparer as IComparer<K> == Comparer<K>.Default)
            Router<K>.To.IPNSort(keys, items);
        else
            IPN.Cmp<K, IComparer<K>>.Sort(keys, items, (IComparer<K>?)comparer ?? Comparer<K>.Default);
    }
}

/// <see href="https://github.com/Voultapher/sort-research-rs/tree/main/ipnsort/src" />
[TemplateClass(IsUnstable = true)]
internal abstract partial class IPN : SortBase
{
    /// Optimal number of comparisons, and good perf.
    const int SmallSortFallbackThreshold = 16;
    const int SmallSortGeneralThreshold = 32;
    /// [`small_sort_general`] uses [`sort8_stable`] as primitive and does a kind of ping-pong merge,
    /// where the output of the first two [`sort8_stable`] calls is stored at the end of the scratch
    /// buffer. This simplifies panic handling and avoids additional copies. This affects the required
    /// scratch buffer size.
    const int SmallSortGeneralScratchLen = SmallSortGeneralThreshold + 16;

    const int SmallSortNetworkThreshold = 32;
    const int SmallSortNetworkScratchLen = SmallSortNetworkThreshold;

    /// Using a stack array, could cause a stack overflow if the type `T` is very large. To be
    /// conservative we limit the usage of small-sorts that require a stack array to types that fit
    /// within this limit.
    const int MaxStackArraySize = 4096;

#if NET8_0_OR_GREATER
    [InlineArray(SmallSortGeneralScratchLen)]
    partial struct ScratchG<T>
    {
        private T _element;
    }
    [InlineArray(SmallSortNetworkScratchLen)]
    partial struct ScratchN<T>
    {
        private T _element;
    }
#else
    [GenerateInlineArray(nameof(T), SmallSortGeneralScratchLen)]
    ref partial struct ScratchG<T>;
    [GenerateInlineArray(nameof(T), SmallSortNetworkScratchLen)]
    ref partial struct ScratchN<T>;
#endif

    internal sealed new partial class Fn<T> : SortBase.Fn<T>
    {
        [Template(nameof(T), "comp", Switch = TemplateVariants.IComparisonOperators)]
        static int SmallSortThreshold() => Unsafe.SizeOf<T>() * SmallSortGeneralScratchLen > MaxStackArraySize
            ? SmallSortFallbackThreshold
            : SmallSortGeneralThreshold;

        [Template(nameof(T), nameof(comp), nameof(span), Switch = TemplateVariants.IComparisonOperators)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void SmallSort(Span<T> span, Comparison<T> comp)
        {
            if (Unsafe.SizeOf<T>() * SmallSortGeneralScratchLen > MaxStackArraySize)
            {
                InsertionSort(ref span.Ref(0), span.Length, comp);
                return;
            }

            Debug.Assert(Unsafe.SizeOf<T>() * SmallSortGeneralScratchLen <= 1 << 16); // 1 MiB limit of CLR

            if (span.Length < 2)
                return;

            int len2 = span.Length / 2;
            ref T first = ref span.Ref(0);
            ScratchG<T> scratch = new();
#if NET8_0_OR_GREATER
            ref T scr = ref ((Span<T>)scratch).Ref(0);
#else
            ref T scr = ref scratch.Ref(0);
#endif

            int sorted;
            if (Unsafe.SizeOf<T>() <= 16 && span.Length >= 16)
            {
                Sort8(in first, ref scr, ref Unsafe.Add(ref scr, span.Length), comp);
                Sort8(in Unsafe.Add(ref first, len2),
                    ref Unsafe.Add(ref scr, len2),
                    ref Unsafe.Add(ref scr, span.Length + 8),
                    comp);
                sorted = 8;
            }
            else if (span.Length >= 8)
            {
                Sort4(in first, ref scr, comp);
                Sort4(in Unsafe.Add(ref first, len2), ref Unsafe.Add(ref scr, len2), comp);
                sorted = 4;
            }
            else
            {
                scr = first;
                Unsafe.Add(ref scr, len2) = Unsafe.Add(ref first, len2);
                sorted = 1;
            }

            for (int half = 0; half < 2; half++)
            {
                int offset = half * len2;

                ref T src = ref Unsafe.Add(ref first, offset);
                ref T dst = ref Unsafe.Add(ref scr, offset);
                int desired = offset == 0 ? len2 : span.Length - len2;

                for (int i = sorted; i < desired; i++)
                {
                    Unsafe.Add(ref dst, i) = Unsafe.Add(ref src, i);
                    InsertTail(ref dst, i, comp);
                }
            }

            try
            {
                MergeBidirectional(in scr, ref first, span.Length, comp);
            }
            catch
            {
#if NET8_0_OR_GREATER
                ((ReadOnlySpan<T>)scratch).Sub(0, span.Length).CopyTo(span);
#else
                scratch.CopyToFill(span);
#endif
                throw;
            }
        }

        /// Sorts range [begin, tail] assuming [begin, tail) is already sorted.
        [Template(nameof(T), nameof(comp), nameof(head), Switch = TemplateVariants.IComparisonOperators)]
        static void InsertTail(ref T head, int offset, Comparison<T> comp)
        {
            Debug.Assert(offset > 0);

            ref T tail = ref Unsafe.Add(ref head, offset);
            ref T sift = ref Unsafe.Prev(ref tail);
            if (!Less(in tail, in sift, comp))
                return;

            T tmp = tail;
            try
            {
                do
                {
                    tail = sift;
                    tail = ref sift;
                    if (Unsafe.AreSame(in sift, in head))
                        break;
                    sift = ref Unsafe.Prev(ref sift);
                }
                while (Less(in tmp, in sift, comp));
            }
            finally
            {
                tail = tmp;
            }
        }

        [Template(nameof(T), nameof(comp), nameof(src), nameof(dst), Switch = TemplateVariants.IComparisonOperators)]
        static void Sort4(ref readonly T src, ref T dst, Comparison<T> comp)
        {
            // By limiting select to picking pointers, we are guaranteed good cmov code-gen
            // regardless of type T's size. Further this only does 5 instead of 6
            // comparisons compared to a stable transposition 4 element sorting-network,
            // and always copies each element exactly once.
            bool c1 = Less(in Unsafe.ROAdd(in src, 1), in src, comp);
            bool c2 = Less(in Unsafe.ROAdd(in src, 3), in Unsafe.ROAdd(in src, 2), comp);
            ref readonly T a = ref Unsafe.ROAdd(in src, c1 ? 1 : 0);
            ref readonly T b = ref Unsafe.ROAdd(in src, !c1 ? 1 : 0);
            ref readonly T c = ref Unsafe.ROAdd(in src, 2 + (c2 ? 1 : 0));
            ref readonly T d = ref Unsafe.ROAdd(in src, 2 + (!c2 ? 1 : 0));

            // Compare (a, c) and (b, d) to identify max/min. We're left with two
            // unknown elements, but because we are a stable sort we must know which
            // one is leftmost and which one is rightmost.
            // c3, c4 | min max unknown_left unknown_right
            //  0,  0 |  a   d    b         c
            //  0,  1 |  a   b    c         d
            //  1,  0 |  c   d    a         b
            //  1,  1 |  c   b    a         d
            bool c3 = Less(in c, in a, comp);
            bool c4 = Less(in d, in b, comp);
            ref readonly T min = ref (c3 ? ref c : ref a);
            ref readonly T max = ref (c4 ? ref b : ref d);
            ref readonly T left = ref (c3 ? ref a : ref (c4 ? ref c : ref b));
            ref readonly T right = ref (c4 ? ref d : ref (c3 ? ref b : ref c));

            // Sort the last two unknown elements.
            bool c5 = Less(in right, in left, comp);
            ref readonly T lo = ref (c5 ? ref right : ref left);
            ref readonly T hi = ref (c5 ? ref left : ref right);

            dst = min;
            Unsafe.Add(ref dst, 1) = lo;
            Unsafe.Add(ref dst, 2) = hi;
            Unsafe.Add(ref dst, 3) = max;
        }

        [Template(nameof(T), nameof(comp), nameof(src), nameof(dst), nameof(scratch), Switch = TemplateVariants.IComparisonOperators)]
        static void Sort8(ref readonly T src, ref T dst, ref T scratch, Comparison<T> comp)
        {
            Sort4(in src, ref scratch, comp);
            Sort4(in Unsafe.ROAdd(in src, 4), ref Unsafe.Add(ref scratch, 4), comp);
            MergeBidirectional(in scratch, ref dst, 8, comp);
        }

        /// Merge v assuming v[..len / 2] and v[len / 2..] are sorted.
        ///
        /// Original idea for bi-directional merging by Igor van den Hoven (quadsort),
        /// adapted to only use merge up and down. In contrast to the original
        /// parity_merge function, it performs 2 writes instead of 4 per iteration.
        [Template(nameof(T), nameof(comp), nameof(src), nameof(dst))]
        internal static void MergeBidirectional(ref readonly T src, ref T dst/*, int split*/, int length, Comparison<T> comp)
        {
            // It helps to visualize the merge:
            //
            // Initial:
            //
            //  |dst (in dst)
            //  |left               |right
            //  v                   v
            // [xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx]
            //                     ^                   ^
            //                     |left_rev           |right_rev
            //                                         |dst_rev (in dst)
            //
            // After:
            //
            //                      |dst (in dst)
            //        |left         |           |right
            //        v             v           v
            // [xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx]
            //       ^             ^           ^
            //       |left_rev     |           |right_rev
            //                     |dst_rev (in dst)
            //
            // In each iteration one of left or right moves up one position, and one of
            // left_rev or right_rev moves down one position, whereas dst always moves
            // up one position and dst_rev always moves down one position. Assuming
            // the input was sorted and the comparison function is correctly implemented
            // at the end we will have left == left_rev + 1, and right == right_rev + 1,
            // fully consuming the input having written it to dst.

            int len2 = Math.DivRem(length, 2, out int rem2);
            //Debug.Assert(split == len2 || split + len2 == length);
            ref readonly T left = ref src;
            ref readonly T right = ref Unsafe.ROAdd(in src, len2); //split

            ref readonly T leftZ = ref Unsafe.ROPrev(in right);
            ref readonly T rightZ = ref Unsafe.ROAdd(in src, length - 1);
            ref T dstZ = ref Unsafe.Add(ref dst, length - 1);

            for (int i = 0; i < len2; i++)
            {
                // merge_up
                bool l = !Less(in right, in left, comp);
                dst = l ? left : right;
                left = ref Unsafe.ROAdd(in left, l ? 1 : 0);
                right = ref Unsafe.ROAdd(in right, l ? 0 : 1);
                dst = ref Unsafe.Add(ref dst, 1);
                // merge_down
                l = !Less(in rightZ, in leftZ, comp);
                dstZ = l ? rightZ : leftZ;
                leftZ = ref Unsafe.ROAdd(in leftZ, l ? 0 : -1);
                rightZ = ref Unsafe.ROAdd(in rightZ, l ? -1 : 0);
                dstZ = ref Unsafe.Add(ref dstZ, -1);
            }

            leftZ = ref Unsafe.RONext(in leftZ);
            rightZ = ref Unsafe.RONext(in rightZ);

            // Odd length, so one element is left unconsumed in the input.
            if (rem2 != 0)
            {
                bool l = Unsafe.IsAddressLessThan(in left, in leftZ);
                dst = l ? left : right;
                left = ref Unsafe.ROAdd(in left, l ? 1 : 0);
                right = ref Unsafe.ROAdd(in right, l ? 0 : 1);
            }

            // We now should have consumed the full input exactly once. This can
            // only fail if the comparison operator fails to be Ord, in which case
            // we will panic and never access the inconsistent state in dst.
            Ensure(Unsafe.AreSame(in left, in leftZ) && Unsafe.AreSame(in right, in rightZ));
        }
    }

    partial class Op<T> : SortBase.Op<T>
        where T : unmanaged,
#if NET7_0_OR_GREATER
        IComparisonOperators<T, T, bool>
#else
        IComparable<T>
#endif
    {
        static int SmallSortThreshold() => SmallSortNetworkThreshold;

        [Template(nameof(T), null, nameof(span))]
        static void SmallSort(Span<T> span)
        {
            // This implementation is tuned to be efficient for integer types.
            if (span.Length < 2)
                return;

            Ensure(span.Length <= SmallSortNetworkScratchLen);

            int len2 = span.Length / 2;
            bool noMerge = span.Length < 18;

            ref T first = ref span.Ref(0);
            int initial = noMerge ? span.Length : len2;
            Span<T> region = span.Sub(0, initial);

            while (true)
            {
                int sorted = 1;
                if (region.Length >= 13)
                {
                    Sort13U(region);
                    sorted = 13;

                }
                else if (region.Length >= 9)
                {
                    Sort9U(region);
                    sorted = 9;
                }

                InsertionSort(ref region.Ref(0), region.Length, offset: sorted - 1);

                if (noMerge)
                    return;

                if (!Unsafe.AreSame(in region.Ref(0), in first))
                    break;

                region = span.Sub(len2, span.Length);
            }

            ScratchN<T> scratch = new();
#if NET8_0_OR_GREATER
            ref T scr = ref ((Span<T>)scratch).Ref(0);
#else
            ref T scr = ref scratch.Ref(0);
#endif

            MergeBidirectional(in first, ref scr, span.Length);

#if NET8_0_OR_GREATER
            ((ReadOnlySpan<T>)scratch).Sub(0, span.Length).CopyTo(span);
#else
            scratch.CopyToFill(span);
#endif
        }

        // Never inline this function to avoid code bloat. It still optimizes nicely and has practically no
        // performance impact.
        [Template(nameof(T), null, nameof(span))]
        static void Sort9U(Span<T> span)
        {
            Debug.Assert(span.Length >= 9);
            // Optimal sorting network see:
            // https://bertdobbelaere.github.io/sorting_networks.html.
            Sort2U(ref span.Ref(0), ref span.Ref(3));
            Sort2U(ref span.Ref(1), ref span.Ref(7));
            Sort2U(ref span.Ref(2), ref span.Ref(5));
            Sort2U(ref span.Ref(4), ref span.Ref(8));
            Sort2U(ref span.Ref(0), ref span.Ref(7));
            Sort2U(ref span.Ref(2), ref span.Ref(4));
            Sort2U(ref span.Ref(3), ref span.Ref(8));
            Sort2U(ref span.Ref(5), ref span.Ref(6));
            Sort2U(ref span.Ref(0), ref span.Ref(2));
            Sort2U(ref span.Ref(1), ref span.Ref(3));
            Sort2U(ref span.Ref(4), ref span.Ref(5));
            Sort2U(ref span.Ref(7), ref span.Ref(8));
            Sort2U(ref span.Ref(1), ref span.Ref(4));
            Sort2U(ref span.Ref(3), ref span.Ref(6));
            Sort2U(ref span.Ref(5), ref span.Ref(7));
            Sort2U(ref span.Ref(0), ref span.Ref(1));
            Sort2U(ref span.Ref(2), ref span.Ref(4));
            Sort2U(ref span.Ref(3), ref span.Ref(5));
            Sort2U(ref span.Ref(6), ref span.Ref(8));
            Sort2U(ref span.Ref(2), ref span.Ref(3));
            Sort2U(ref span.Ref(4), ref span.Ref(5));
            Sort2U(ref span.Ref(6), ref span.Ref(7));
            Sort2U(ref span.Ref(1), ref span.Ref(2));
            Sort2U(ref span.Ref(3), ref span.Ref(4));
            Sort2U(ref span.Ref(5), ref span.Ref(6));
        }

        // Never inline this function to avoid code bloat. It still optimizes nicely and has practically no
        // performance impact.
        [Template(nameof(T), null, nameof(span))]
        static void Sort13U(Span<T> span)
        {
            Debug.Assert(span.Length >= 13);
            // Optimal sorting network see:
            // https://bertdobbelaere.github.io/sorting_networks.html.
            Sort2U(ref span.Ref(0), ref span.Ref(12));
            Sort2U(ref span.Ref(1), ref span.Ref(10));
            Sort2U(ref span.Ref(2), ref span.Ref(9));
            Sort2U(ref span.Ref(3), ref span.Ref(7));
            Sort2U(ref span.Ref(5), ref span.Ref(11));
            Sort2U(ref span.Ref(6), ref span.Ref(8));
            Sort2U(ref span.Ref(1), ref span.Ref(6));
            Sort2U(ref span.Ref(2), ref span.Ref(3));
            Sort2U(ref span.Ref(4), ref span.Ref(11));
            Sort2U(ref span.Ref(7), ref span.Ref(9));
            Sort2U(ref span.Ref(8), ref span.Ref(10));
            Sort2U(ref span.Ref(0), ref span.Ref(4));
            Sort2U(ref span.Ref(1), ref span.Ref(2));
            Sort2U(ref span.Ref(3), ref span.Ref(6));
            Sort2U(ref span.Ref(7), ref span.Ref(8));
            Sort2U(ref span.Ref(9), ref span.Ref(10));
            Sort2U(ref span.Ref(11), ref span.Ref(12));
            Sort2U(ref span.Ref(4), ref span.Ref(6));
            Sort2U(ref span.Ref(5), ref span.Ref(9));
            Sort2U(ref span.Ref(8), ref span.Ref(11));
            Sort2U(ref span.Ref(10), ref span.Ref(12));
            Sort2U(ref span.Ref(0), ref span.Ref(5));
            Sort2U(ref span.Ref(3), ref span.Ref(8));
            Sort2U(ref span.Ref(4), ref span.Ref(7));
            Sort2U(ref span.Ref(6), ref span.Ref(11));
            Sort2U(ref span.Ref(9), ref span.Ref(10));
            Sort2U(ref span.Ref(0), ref span.Ref(1));
            Sort2U(ref span.Ref(2), ref span.Ref(5));
            Sort2U(ref span.Ref(6), ref span.Ref(9));
            Sort2U(ref span.Ref(7), ref span.Ref(8));
            Sort2U(ref span.Ref(10), ref span.Ref(11));
            Sort2U(ref span.Ref(1), ref span.Ref(3));
            Sort2U(ref span.Ref(2), ref span.Ref(4));
            Sort2U(ref span.Ref(5), ref span.Ref(6));
            Sort2U(ref span.Ref(9), ref span.Ref(10));
            Sort2U(ref span.Ref(1), ref span.Ref(2));
            Sort2U(ref span.Ref(3), ref span.Ref(4));
            Sort2U(ref span.Ref(5), ref span.Ref(7));
            Sort2U(ref span.Ref(6), ref span.Ref(8));
            Sort2U(ref span.Ref(2), ref span.Ref(3));
            Sort2U(ref span.Ref(4), ref span.Ref(5));
            Sort2U(ref span.Ref(6), ref span.Ref(7));
            Sort2U(ref span.Ref(8), ref span.Ref(9));
            Sort2U(ref span.Ref(3), ref span.Ref(4));
            Sort2U(ref span.Ref(5), ref span.Ref(6));
        }
    }

    // Recursively select a pseudomedian if above this threshold.
    private const int PsuedoMedianRecThreshold = 64;

    partial class Fn<T>
    {
        /// Selects a pivot from `v`. Algorithm taken from glidesort by Orson Peters.
        ///
        /// This chooses a pivot by sampling an adaptive amount of points, approximating
        /// the quality of a median of sqrt(n) elements.
        [Template(nameof(T), nameof(comp))]
        static int ChoosePivot(ReadOnlySpan<T> span, Comparison<T> comp)
        {
            int len = span.Length;
            Ensure(len >= 8);

            int len8 = len / 8;
            ref readonly T a = ref span.Ref(0);
            ref readonly T b = ref span.Ref(len8 * 4);
            ref readonly T c = ref span.Ref(len8 * 7);

            if (len < PsuedoMedianRecThreshold)
                return span.Offset(in Median3(in a, in b, in c, comp));
            else
                return span.Offset(in Median3Rec(in a, in b, in c, len8, comp));
        }

        /// Calculates an approximate median of 3 elements from sections a, b, c, or
        /// recursively from an approximation of each, if they're large enough. By
        /// dividing the size of each section by 8 when recursing we have logarithmic
        /// recursion depth and overall sample from f(n) = 3*f(n/8) -> f(n) =
        /// O(n^(log(3)/log(8))) ~= O(n^0.528) elements.
        ///
        /// SAFETY: a, b, c must point to the start of initialized regions of memory of
        /// at least n elements.
        [Template(nameof(T), nameof(comp))]
        static ref readonly T Median3Rec(ref readonly T a, ref readonly T b, ref readonly T c, int n, Comparison<T> comp)
        {
            // SAFETY: a, b, c still point to initialized regions of n / 8 elements,
            // by the exact same logic as in choose_pivot.
            if (n * 8 >= PsuedoMedianRecThreshold)
            {
                var n8 = n / 8;
                a = ref Median3Rec(in a, in Unsafe.ROAdd(in a, n8 * 4), in Unsafe.ROAdd(in a, n8 * 7), n8, comp);
                b = ref Median3Rec(in b, in Unsafe.ROAdd(in b, n8 * 4), in Unsafe.ROAdd(in b, n8 * 7), n8, comp);
                c = ref Median3Rec(in c, in Unsafe.ROAdd(in c, n8 * 4), in Unsafe.ROAdd(in c, n8 * 7), n8, comp);
            }
            return ref Median3(in a, in b, in c, comp);
        }

        /// Calculates the median of 3 elements.
        ///
        /// SAFETY: a, b, c must be valid initialized elements.
        [Template(nameof(T), nameof(comp))]
        static ref readonly T Median3(ref readonly T a, ref readonly T b, ref readonly T c, Comparison<T> comp)
        {
            var x = Less(in a, in b, comp);
            var y = Less(in a, in c, comp);
            if (x == y)
            {
                var z = Less(in b, in c, comp);
                return ref (z ^ x ? ref c : ref b);
            }
            else
                return ref a;
        }

        [Template(nameof(T), nameof(comp), nameof(span))]
        static void QuickSort(Span<T> span, [AllowNull] ref readonly T pivotAncestor, int limit, Comparison<T> comp)
        {
            while (true)
            {
                if (span.Length <= SmallSortThreshold())
                {
                    SmallSort(span, comp);
                    return;
                }

                // If too many bad pivot choices were made, simply fall back to heapsort in order to
                // guarantee `O(N x log(N))` worst-case.
                if (limit == 0)
                {
                    HeapSort(span, comp);
                    return;
                }

                limit--;

                // Choose a pivot and try guessing whether the slice is already sorted.
                int p = ChoosePivot(span, comp);
                int numLT;

                // If the chosen pivot is equal to the predecessor, then it's the smallest element in the
                // slice. Partition the slice into elements equal to and elements greater than the pivot.
                // This case is usually hit when the slice contains many duplicate elements.
                if (!Unsafe.IsNullRef(in pivotAncestor))
                {
                    if (!Less(in pivotAncestor!, in span.Ref(p), comp))
                    {
                        numLT = PartitionLE(span, p, comp);
                        // Continue sorting elements greater than the pivot. We know that `num_lt` contains
                        // the pivot. So we can continue after `num_lt`.
                        span = span.Sub(numLT + 1, span.Length);
                        pivotAncestor = ref Unsafe.NullRef<T>();
                        continue;
                    }
                }

                // Partition the slice.
                numLT = Partition(span, p, comp);
                Debug.Assert(numLT < span.Length);

                // Recurse into the left side. We have a fixed recursion limit, testing shows no real
                // benefit for recursing into the shorter side.
                QuickSort(span.Sub(0, numLT), in pivotAncestor, limit, comp);

                pivotAncestor = ref span.Ref(numLT);
                span = span.Sub(numLT + 1, span.Length);
            }
        }
    }

    // Specialize for types that are relatively cheap to copy, where branchless optimizations
    // have large leverage e.g. `u64` and `String`.
    private const int MaxBranchlessPartitionSize = 96;
    private const int ReferenceCopyCostMultiplier = 4;

    static class TypeTraits<T>
    {
        internal static int EstimatedCost
            => typeof(T).IsValueType
                ? RuntimeHelpers.IsReferenceOrContainsReferences<T>()
                    ? Unsafe.SizeOf<T>() * ReferenceCopyCostMultiplier
                    : Unsafe.SizeOf<T>()
                : IntPtr.Size * ReferenceCopyCostMultiplier;
        internal static readonly bool UseBranchlessPartition = EstimatedCost <= MaxBranchlessPartitionSize;
    }

    static class TypeTraits<T, V>
    {
        internal static readonly bool UseBranchlessPartition = TypeTraits<T>.EstimatedCost + TypeTraits<V>.EstimatedCost <= MaxBranchlessPartitionSize;
    }

    partial class Op<T>
    {
        [Template(nameof(T), null, nameof(span), Switch = TemplateVariants.LessThanOrEqual)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Partition(Span<T> span, int p)
        {
            // Place the pivot at the beginning of slice.
            Swap(ref span.Ref(0), ref span.Ref(p));
            int numLT = PartitionLomutoBranchlessCyclic(span.Sub(1, span.Length), in span.Ref(0));
            Swap(ref span.Ref(0), ref span.Ref(numLT));
            return numLT;
        }
    }

    partial class Fn<T>
    {
        [Template(nameof(T), nameof(comp), nameof(span), Switch = TemplateVariants.LessThanOrEqual | TemplateVariants.IComparisonOperators)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Partition(Span<T> span, int p, Comparison<T> comp)
        {
            // Place the pivot at the beginning of slice.
            Swap(ref span.Ref(0), ref span.Ref(p));
            int numLT = TypeTraits<T>.UseBranchlessPartition
                ? PartitionLomutoBranchlessCyclic(span.Sub(1, span.Length), in span.Ref(0), comp)
                : PartitionHoareBranchyCyclic(span.Sub(1, span.Length), in span.Ref(0), comp);
            Swap(ref span.Ref(0), ref span.Ref(numLT));
            return numLT;
        }

        [Template(nameof(T), nameof(comp), nameof(span), Switch = TemplateVariants.LessThanOrEqual | TemplateVariants.IComparisonOperators)]
        static int PartitionHoareBranchyCyclic(Span<T> span, ref readonly T pivot, Comparison<T> comp)
        {
            if (span.Length == 0)
                return 0;

            // Optimized for large types that are expensive to move. Not optimized for integers. Optimized
            // for small code-gen, assuming that is_less is an expensive operation that generates
            // substantial amounts of code or a call. And that copying elements will likely be a call to
            // memcpy. Using 2 `ptr::copy_nonoverlapping` has the chance to be faster than
            // `ptr::swap_nonoverlapping` because `memcpy` can use wide SIMD based on runtime feature
            // detection. Benchmarks support this analysis.
            ref T left = ref span.Ref(0);
            ref T right = ref span.Ref(span.Length);

            ref T gap = ref Unsafe.NullRef<T>();
            T tmp = default!;
            try
            {
                while (true)
                {
                    // Find the first element greater than the pivot.
                    while (Unsafe.IsAddressLessThan(in left, in right) && Less(in left, in pivot, comp))
                        left = ref Unsafe.Next(ref left);

                    // Find the last element equal to the pivot.
                    do right = ref Unsafe.Prev(ref right);
                    while (Unsafe.IsAddressLessThan(in left, in right) && !Less(in right, in pivot, comp));

                    if (!Unsafe.IsAddressLessThan(in left, in right))
                        break;

                    // Swap the found pair of out-of-order elements via cyclic permutation.
                    if (Unsafe.IsNullRef(ref gap))
                    {
                        tmp = left;
                        gap = ref right;
                    }
                    // Single place where we instantiate ptr::copy_nonoverlapping in the partition.
                    else
                    {
                        gap = left;
                        gap = ref right;
                    }
                    left = right;
                    left = ref Unsafe.Next(ref left);
                }

                return span.Offset(in left);
            }
            finally
            {
                if (!Unsafe.IsNullRef(ref gap))
                {
                    gap = tmp;
                }
            }
        }

        [Template(nameof(T), nameof(comp), nameof(span), Switch = TemplateVariants.LessThanOrEqual)]
        static int PartitionLomutoBranchlessCyclic(Span<T> span, ref readonly T pivot, Comparison<T> comp)
        {
            // Novel partition implementation by Lukas Bergdoll and Orson Peters. Branchless Lomuto
            // partition paired with a cyclic permutation. TODO link writeup.

            if (span.Length == 0)
                return 0;

            // Counts the number of elements that compared less-than, also works around:
            // https://github.com/rust-lang/rust/issues/117128
            ref T left = ref span.Ref(0);
            // The current element that is being looked at, scans left to right through slice.
            ref T right = ref span.Ref(1);
            // Gap guard that tracks the temporary duplicate in the input.
            ref T gap = ref span.Ref(0);
            ref T last = ref span.Ref(span.Length);
            T tmp = left;
            try
            {
                while (Unsafe.IsAddressLessThan(in right, in last))
                {
                    bool less = Less(in right, in pivot, comp);
                    gap = left;
                    left = right;
                    gap = ref right;
                    left = ref Unsafe.Add(ref left, less ? 1 : 0);
                    right = ref Unsafe.Next(ref right);
                }

                {
                    bool less = Less(in tmp, in pivot, comp);
                    gap = left;
                    left = tmp;
                    left = ref Unsafe.Add(ref left, less ? 1 : 0);
                }
            }
            catch
            {
                gap = tmp;
                throw;
            }

            return span.Offset(in left);
        }

        [Template(nameof(T), nameof(comp), nameof(span))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Sort(Span<T> span, Comparison<T> comp)
        {
            Debug.Assert(span.Length >= 2);

            var (run, reversed) = FindExisitingRun(span, comp);

            if (run == span.Length)
            {
                if (reversed)
                    span.Reverse();

                // It would be possible to a do in-place merging here for a long existing streak. But that
                // makes the implementation a lot bigger, users can use `slice::sort` for that use-case.
                return;
            }

            // Limit the number of imbalanced partitions to `2 * floor(log2(len))`.
            // The binary OR by one is used to eliminate the zero-check in the logarithm.
            int limit = 2 * BitOperations.Log2((uint)span.Length);
            QuickSort(span, in Unsafe.NullRef<T>(), limit, comp); // 
        }

        /// Finds a run of sorted elements starting at the beginning of the slice.
        ///
        /// Returns the length of the run, and a bool that is false when the run
        /// is ascending, and true if the run strictly descending.
        [Template(nameof(T), nameof(comp))]
        static (int, bool) FindExisitingRun(Span<T> span, Comparison<T> comp)
        {
            int run = 2;
            bool less = Less(in span.Ref(1), in span.Ref(0), comp);
            if (less)
                while (run < span.Length && Less(in span.Ref(run), in span.Ref(run - 1), comp))
                    run++;
            else
                while (run < span.Length && !Less(in span.Ref(run), in span.Ref(run - 1), comp))
                    run++;
            return (run, less);
        }
    }
}
