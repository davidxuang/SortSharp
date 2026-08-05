using System;
using System.Collections.Generic;
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
    public static void PDQSort<T>(this Span<T> span)
        where T : unmanaged, INumber<T>
    {
        if (span.Length <= 1)
            return;
        else if (Router<T>.IsFloatingPointIeee)
            Router<T>.To.PDQSort(span);
        else
            PDQ.Op<T>.Sort(span);
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PDQSort<T>(this Span<T> span, IComparer<T>? comparer = null)
        => PDQSort<T, IComparer<T>?>(span, comparer);

    public static void PDQSort<T>(this Span<T> span, Comparison<T> compare)
    {
        ArgumentNullException.ThrowIfNull(compare, nameof(compare));
        if (span.Length <= 1)
            return;

        PDQ.Fn<T>.Sort(span, compare);
    }

    public static void PDQSort<T, TComparer>(this Span<T> span, TComparer comparer)
        where TComparer : IComparer<T>?
    {
        if (span.Length <= 1)
            return;
        else if (typeof(TComparer).IsValueType)
#pragma warning disable CS8631
            PDQ.Cmp<T, TComparer>.Sort(span, comparer);
#pragma warning restore CS8631
        else if (comparer is null || comparer as IComparer<T> == Comparer<T>.Default)
            Router<T>.To.PDQSort(span);
        else
            PDQ.Cmp<T, IComparer<T>>.Sort(span, comparer);
    }

#if NET7_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PDQSort<K, V>(this Span<K> keys, Span<V> items)
        where K : unmanaged, INumber<K>
    {
        ArgumentOutOfRangeException.ThrowIf(keys.Length != items.Length, nameof(items));

        if (keys.Length <= 1)
            return;
        else if (Router<K>.IsFloatingPointIeee)
            Router<K>.To.PDQSort(keys, items);
        else
            PDQ.Op<K>.Sort(keys, items); 
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PDQSort<K, V>(this Span<K> keys, Span<V> items, IComparer<K>? comparer = null)
        => PDQSort<K, V, IComparer<K>?>(keys, items, comparer);

    public static void PDQSort<K, V>(this Span<K> keys, Span<V> items, Comparison<K> compare)
    {
        ArgumentNullException.ThrowIfNull(compare, nameof(compare));
        ArgumentOutOfRangeException.ThrowIf(keys.Length != items.Length, nameof(items));

        if (keys.Length <= 1)
            return;
        PDQ.Fn<K>.Sort(keys, items, compare);
    }

    public static void PDQSort<K, V, TComparer>(this Span<K> keys, Span<V> items, TComparer comparer)
        where TComparer : IComparer<K>?
    {
        ArgumentOutOfRangeException.ThrowIf(keys.Length != items.Length, nameof(items));

        if (keys.Length <= 1)
            return;
        else if (typeof(TComparer).IsValueType)
#pragma warning disable CS8631
            PDQ.Cmp<K, TComparer>.Sort(keys, items, comparer);
#pragma warning restore CS8631
        else if (comparer is null || comparer as IComparer<K> == Comparer<K>.Default)
            Router<K>.To.PDQSort(keys, items);
        else
            PDQ.Cmp<K, IComparer<K>>.Sort(keys, items, (IComparer<K>?)comparer ?? Comparer<K>.Default);
    }
}

/// <see cref="https://github.com/orlp/pdqsort"/>
/// <seealso cref="https://gist.github.com/hez2010/6b52929ee1755788c34818972c46aefb"/>
[TemplateClass(IsUnstable = true)]
internal abstract partial class PDQ : SortBase
{
    // Partitions below this size are sorted using insertion sort.
    private const int InsertionSortThreshold = 24;
    // Partitions above this size use Tukey's ninther to select the pivot.
    private const int NintherThreshold = 128;
    // When we detect an already sorted partition, attempt an insertion sort that allows this
    // amount of element moves before giving up.
    private const int PartialInsertionSortLimit = 8;
    // Must be multiple of 8 due to loop unrolling, and < 256 to fit in unsigned char.
    private const byte BlockSize = 64;
    // Cacheline size, assumes power of two.
    private const byte CacheLineSize = 64;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe Span<byte> AlignCacheline(Span<byte> span)
    {
        var p = (nuint)Unsafe.AsPointer(ref span[0]);
        p = (p + CacheLineSize - 1) & ~((nuint)CacheLineSize - 1);
        return new Span<byte>((void*)p, CacheLineSize);
    }

    internal new sealed partial class Fn<T> : SortBase.Fn<T>
    {
        // Sorts [start, end) using insertion sort with the given comparison function. Assumes
        // *(start - 1) is an element smaller than or equal to any element in [start, end).
        [Template(nameof(T), nameof(comp), nameof(first))]
        static void UnguardedInsertionSort(ref T first, int length, Comparison<T> comp)
        {
            if (length <= 1) return;

            ref T curr = ref first;
            while (--length != 0)
            {
                curr = ref Unsafe.Next(ref curr);
                ref T sift = ref curr;
                ref T sift1 = ref Unsafe.Prev(ref curr);

                // Compare first so we can avoid 2 moves for an element already positioned correctly.
                if (Less(in sift, in sift1, comp))
                {
                    T tmp = sift;
                    do
                    {
#if DEBUG
                        Ensure(!Unsafe.IsAddressGreaterThan(ref first, ref sift1));
#endif
                        sift = sift1;
                        sift = ref Unsafe.Prev(ref sift);
                        sift1 = ref Unsafe.Prev(ref sift1);
                    }
                    while (Less(in tmp, in sift1, comp));

                    sift = tmp;
                }
            }
        }

        // Attempts to use insertion sort on [start, end). Will return false if more than
        // partial_insertion_sort_limit elements were moved, and abort sorting. Otherwise it will
        // successfully sort and return true.
        [Template(nameof(T), nameof(comp), nameof(first))]
        static bool PartialInsertionSort(ref T first, int length, Comparison<T> comp)
        {
            if (length <= 1) return true;

            var limit = 0;
            ref T curr = ref first;
            while (--length != 0)
            {
                curr = ref Unsafe.Next(ref curr);
                ref T sift = ref curr;
                ref T sift1 = ref Unsafe.Prev(ref curr);

                // Compare first so we can avoid 2 moves for an element already positioned correctly.
                if (Less(in sift, in sift1, comp))
                {
                    T tmp = sift;
                    do
                    {
                        sift = sift1;
                        sift = ref Unsafe.Prev(ref sift);
                        limit++;

                        if (Unsafe.AreSame(ref sift, ref first)) break;
                        sift1 = ref Unsafe.Prev(ref sift1);
                    }
                    while (Less(in tmp, in sift1, comp));

                    sift = tmp;
                }

                if (limit > PartialInsertionSortLimit) return false;
            }

            return true;
        }
    }

    partial class Op<T>
    {
        [Template(nameof(T), null, nameof(first), nameof(last))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SwapOffsets(ref T first, ref T last, ReadOnlySpan<byte> offsetsL, ReadOnlySpan<byte> offsetsR, int num, bool useSwaps)
        {
            if (useSwaps)
            {
                // This case is needed for the descending distribution, where we need
                // to have proper swapping for pdqsort to remain O(n).
                for (int i = 0; i < num; i++)
                {
                    Swap(
                        ref Unsafe.Add(ref first, offsetsL.Ref(i)),
                        ref Unsafe.Add(ref last, -offsetsR.Ref(i)));
                }
            }
            else if (num > 0)
            {
                ref T l = ref Unsafe.Add(ref first, offsetsL.Ref(0));
                ref T r = ref Unsafe.Add(ref last, -offsetsR.Ref(0));
                T tmp = l;
                l = r;
                for (int i = 1; i < num; i++)
                {
                    l = ref Unsafe.Add(ref first, offsetsL.Ref(i));
                    r = l;
                    r = ref Unsafe.Add(ref last, -offsetsR.Ref(i));
                    l = r;
                }
                r = tmp;
            }
        }

        // Partitions [start, end) around pivot *start using comparison function comp. Elements equal
        // to the pivot are put in the right-hand partition. Returns the position of the pivot after
        // partitioning and whether the passed sequence already was correctly partitioned. Assumes the
        // pivot is a median of at least 3 elements and that [start, end) is at least
        // insertion_sort_threshold long. Uses branchless partitioning.
        [Template(nameof(T), null, nameof(span))]
        static (int Pivot, bool HasPartitioned) PartitionRight(Span<T> span)
        {
            // Move pivot into local for speed.
            T pivot = span.Ref(0);
            ref T first = ref span.Ref(0);
            ref T last = ref span.Ref(span.Length);

            // Find the first element greater than or equal than the pivot (the median of 3 guarantees
            // this exists).
            do first = ref Unsafe.Next(ref first);
            while (Less(in first, in pivot));

            // Find the first element strictly smaller than the pivot. We have to guard this search if
            // there was no element before *first.
            if (Unsafe.AreSame(ref Unsafe.Prev(ref first), ref span.Ref(0)))
            {
                if (Unsafe.IsAddressLessThan(ref first, ref last))
                    do last = ref Unsafe.Prev(ref last);
                    while (!Less(in last, in pivot) && Unsafe.IsAddressLessThan(ref first, ref last));
            }
            else
            {
                do last = ref Unsafe.Prev(ref last);
                while (!Less(in last, in pivot));
            }

            // If the first pair of elements that should be swapped to partition are the same element,
            // the passed in sequence already was correctly partitioned.
            bool hasPartitioned = !Unsafe.IsAddressLessThan(ref first, ref last);

            if (!hasPartitioned)
            {
                Swap(ref first, ref last);
                first = ref Unsafe.Next(ref first);

                // The following branchless partitioning is derived from "BlockQuicksort: How Branch
                // Mispredictions don’t affect Quicksort" by Stefan Edelkamp and Armin Weiss, but
                // heavily micro-optimized.
                var offsetsL = AlignCacheline(stackalloc byte[BlockSize + CacheLineSize]);
                var offsetsR = AlignCacheline(stackalloc byte[BlockSize + CacheLineSize]);

                ref T offsetsLBase = ref first;
                ref T offsetsRBase = ref last;
                int numL = 0, numR = 0, startL = 0, startR = 0;

                while (Unsafe.IsAddressLessThan(ref first, ref last))
                {
                    // Fill up offset blocks with elements that are on the wrong side.
                    // First we determine how much elements are considered for each offset block.
                    int numUnknown = Unsafe.Offset(in first, in last);
                    int leftSplit = numL == 0 ? (numR == 0 ? numUnknown / 2 : numUnknown) : 0;
                    int rightSplit = numR == 0 ? (numUnknown - leftSplit) : 0;

                    // Fill the offset blocks.
                    if (leftSplit >= BlockSize)
                    {
                        for (byte i = 0; i < BlockSize;)
                        {
                            offsetsL[numL] = i++; numL += !Less(in first, in pivot) ? 1 : 0; first = ref Unsafe.Next(ref first);
                            offsetsL[numL] = i++; numL += !Less(in first, in pivot) ? 1 : 0; first = ref Unsafe.Next(ref first);
                            offsetsL[numL] = i++; numL += !Less(in first, in pivot) ? 1 : 0; first = ref Unsafe.Next(ref first);
                            offsetsL[numL] = i++; numL += !Less(in first, in pivot) ? 1 : 0; first = ref Unsafe.Next(ref first);
                            offsetsL[numL] = i++; numL += !Less(in first, in pivot) ? 1 : 0; first = ref Unsafe.Next(ref first);
                            offsetsL[numL] = i++; numL += !Less(in first, in pivot) ? 1 : 0; first = ref Unsafe.Next(ref first);
                            offsetsL[numL] = i++; numL += !Less(in first, in pivot) ? 1 : 0; first = ref Unsafe.Next(ref first);
                            offsetsL[numL] = i++; numL += !Less(in first, in pivot) ? 1 : 0; first = ref Unsafe.Next(ref first);
                        }
                    }
                    else
                    {
                        for (byte i = 0; i < leftSplit;)
                        {
                            offsetsL[numL] = i++; numL += !Less(in first, in pivot) ? 1 : 0; first = ref Unsafe.Next(ref first);
                        }
                    }

                    if (rightSplit >= BlockSize)
                    {
                        for (byte i = 0; i < BlockSize;)
                        {
                            offsetsR[numR] = ++i; last = ref Unsafe.Prev(ref last); numR += Less(in last, in pivot) ? 1 : 0;
                            offsetsR[numR] = ++i; last = ref Unsafe.Prev(ref last); numR += Less(in last, in pivot) ? 1 : 0;
                            offsetsR[numR] = ++i; last = ref Unsafe.Prev(ref last); numR += Less(in last, in pivot) ? 1 : 0;
                            offsetsR[numR] = ++i; last = ref Unsafe.Prev(ref last); numR += Less(in last, in pivot) ? 1 : 0;
                            offsetsR[numR] = ++i; last = ref Unsafe.Prev(ref last); numR += Less(in last, in pivot) ? 1 : 0;
                            offsetsR[numR] = ++i; last = ref Unsafe.Prev(ref last); numR += Less(in last, in pivot) ? 1 : 0;
                            offsetsR[numR] = ++i; last = ref Unsafe.Prev(ref last); numR += Less(in last, in pivot) ? 1 : 0;
                            offsetsR[numR] = ++i; last = ref Unsafe.Prev(ref last); numR += Less(in last, in pivot) ? 1 : 0;
                        }
                    }
                    else
                    {
                        for (byte i = 0; i < rightSplit;)
                        {
                            offsetsR[numR] = ++i; last = ref Unsafe.Prev(ref last); numR += Less(in last, in pivot) ? 1 : 0;
                        }
                    }

                    // Swap elements and update block sizes and first/last boundarie
                    int num = Math.Min(numL, numR);
                    SwapOffsets(
                        ref offsetsLBase, ref offsetsRBase,
                        offsetsL.Sub(startL, CacheLineSize),
                        offsetsR.Sub(startR, CacheLineSize),
                        num, numL == numR);

                    numL -= num; numR -= num;
                    startL += num; startR += num;

                    if (numL == 0)
                    {
                        startL = 0;
                        offsetsLBase = ref first;
                    }

                    if (numR == 0)
                    {
                        startR = 0;
                        offsetsRBase = ref last;
                    }
                }

                // We have now fully identified [first, last)'s proper position. Swap the last elements.
                if (numL != 0)
                {
                    offsetsL = offsetsL.Sub(startL, CacheLineSize);
                    while (numL-- != 0)
                    {
                        last = ref Unsafe.Prev(ref last);
                        Swap(ref Unsafe.Add(ref offsetsLBase, offsetsL[numL]), ref last);
                    }
                    first = ref last;
                }
                if (numR != 0)
                {
                    offsetsR = offsetsR.Sub(startR, CacheLineSize);
                    while (numR-- != 0)
                    {
                        Swap(ref Unsafe.Add(ref offsetsRBase, -offsetsR[numR]), ref first);
                        first = ref Unsafe.Next(ref first);
                    }
                    last = ref first;
                }
            }

            Ensure(!Unsafe.IsAddressGreaterThan(ref first, ref span.Ref(span.Length)));

            // Put the pivot in the right place.
            first = ref Unsafe.Prev(ref first);
            span.Ref(0) = first;
            first = pivot;

            return (span.Offset(in first), hasPartitioned);
        }
    }

    partial class Fn<T>
    {
        // Partitions [start, end) around pivot *start using comparison function comp. Elements equal
        // to the pivot are put in the right-hand partition. Returns the position of the pivot after
        // partitioning and whether the passed sequence already was correctly partitioned. Assumes the
        // pivot is a median of at least 3 elements and that [start, end) is at least
        // insertion_sort_threshold long.
        [Template(nameof(T), nameof(comp), nameof(span), Switch = TemplateVariants.IComparisonOperators)]
        static (int Pivot, bool HasPartitioned) PartitionRight(Span<T> span, Comparison<T> comp)
        {
            // Move pivot into local for speed.
            T pivot = span.Ref(0);
            ref T first = ref span.Ref(0);
            ref T last = ref span.Ref(span.Length);

            // Find the first element greater than or equal than the pivot (the median of 3 guarantees
            // this exists).
            do first = ref Unsafe.Next(ref first);
            while (Less(in first, in pivot, comp));

            // Find the first element strictly smaller than the pivot. We have to guard this search if
            // there was no element before *first.
            if (Unsafe.AreSame(ref Unsafe.Prev(ref first), ref span.Ref(0)))
            {
                if (Unsafe.IsAddressLessThan(ref first, ref last))
                    do last = ref Unsafe.Prev(ref last);
                    while (!Less(in last, in pivot, comp) && Unsafe.IsAddressLessThan(ref first, ref last));
            }
            else
            {
                do last = ref Unsafe.Prev(ref last);
                while (!Less(in last, in pivot, comp));
            }

            // If the first pair of elements that should be swapped to partition are the same element,
            // the passed in sequence already was correctly partitioned.
            bool hasPartitioned = !Unsafe.IsAddressLessThan(ref first, ref last);

            // Keep swapping pairs of elements that are on the wrong side of the pivot. Previously
            // swapped pairs guard the searches, which is why the first iteration is special-cased
            // above.
            while (Unsafe.IsAddressLessThan(ref first, ref last))
            {
                Swap(ref first, ref last);
                do first = ref Unsafe.Next(ref first);
                while (Less(in first, in pivot, comp));
                do last = ref Unsafe.Prev(ref last);
                while (!Less(in last, in pivot, comp));
            }

            Ensure(!Unsafe.IsAddressGreaterThan(ref first, ref span.Ref(span.Length)));

            // Put the pivot in the right place.
            first = ref Unsafe.Prev(ref first);
            span.Ref(0) = first;
            first = pivot;

            return (span.Offset(in first), hasPartitioned);
        }

        // Similar function to the one above, except elements equal to the pivot are put to the left of
        // the pivot and it doesn't check or return if the passed sequence already was partitioned.
        // Since this is rarely used (the many equal case), and in that case pdqsort already has O(n)
        // performance, no block quicksort is applied here for simplicity.
        [Template(nameof(T), nameof(comp), nameof(span))]
        static int PartitionLeft(Span<T> span, Comparison<T> comp)
        {
            T pivot = span.Ref(0);
            ref T first = ref span.Ref(0);
            ref T last = ref span.Ref(span.Length);

            do last = ref Unsafe.Prev(ref last);
            while (Less(in pivot, in last, comp));
            if (Unsafe.AreSame(ref Unsafe.Next(ref last), ref span.Ref(span.Length)))
            {
                if (Unsafe.IsAddressLessThan(ref first, ref last))
                    do first = ref Unsafe.Next(ref first);
                    while (!Less(in pivot, in first, comp) && Unsafe.IsAddressLessThan(ref first, ref last));
            }
            else
            {
                do first = ref Unsafe.Next(ref first);
                while (!Less(in pivot, in first, comp));
            }

            while (Unsafe.IsAddressLessThan(ref first, ref last))
            {
                Swap(ref first, ref last);
                do last = ref Unsafe.Prev(ref last);
                while (Less(in pivot, in last, comp));
                do first = ref Unsafe.Next(ref first);
                while (!Less(in pivot, in first, comp));
            }

            Ensure(!Unsafe.IsAddressLessThan(ref last, ref span.Ref(0)));

            span.Ref(0) = last;
            last = pivot;

            return span.Offset(in last);
        }

        [Template(nameof(T), nameof(comp), nameof(span))]
        static void SortLoop(Span<T> span, Comparison<T> comp, int badAllowed, bool leftmost = true)
        {
            while (true)
            {
                int size = span.Length;

                // Insertion sort is faster for small arrays.
                if (size < InsertionSortThreshold)
                {
                    if (leftmost) InsertionSort(ref span.Ref(0), size, comp);
                    else UnguardedInsertionSort(ref span.Ref(0), size, comp);
                    return;
                }

                // Choose pivot as median of 3 or pseudomedian of 9.
                int mid = size / 2;
                if (size > NintherThreshold)
                {
                    Sort3U(ref span.Ref(0), ref span.Ref(mid), ref span.Ref(size - 1), comp);
                    Sort3U(ref span.Ref(1), ref span.Ref(mid - 1), ref span.Ref(size - 2), comp);
                    Sort3U(ref span.Ref(2), ref span.Ref(mid + 1), ref span.Ref(size - 3), comp);
                    Sort3U(ref span.Ref(mid - 1), ref span.Ref(mid), ref span.Ref(mid + 1), comp);
                    Swap(ref span.Ref(0), ref span.Ref(mid));
                }
                else
                {
                    Sort3U(ref span.Ref(mid), ref span.Ref(0), ref span.Ref(size - 1), comp);
                }

                // If *(start - 1) is the end of the right partition of a previous partition operation
                // there is no element in [start, end) that is smaller than *(start - 1). Then if our
                // pivot compares equal to *(start - 1) we change alloc, putting equal elements in
                // the left partition, greater elements in the right partition. We do not have to
                // recurse on the left partition, since it's sorted (all equal).
                if (!leftmost && !Less(in span.Ref(-1), in span.Ref(0), comp))
                {
                    int start = PartitionLeft(span, comp) + 1;
                    span = span.Sub(start, size);
                    continue;
                }

                var (pivot, hasPartitioned) = PartitionRight(span, comp);

                // Check for a highly unbalanced partition.
                var rightSize = size - (pivot + 1);
                var highlyUnbalanced = pivot < size / 8 || rightSize < size / 8;

                // If we got a highly unbalanced partition we shuffle elements to break many patterns.
                if (highlyUnbalanced)
                {
                    // If we had too many bad partitions, switch to heapsort to guarantee O(n log n).
                    if (--badAllowed == 0)
                    {
                        HeapSort(span, comp);
                        return;
                    }

                    if (pivot >= InsertionSortThreshold)
                    {
                        Swap(ref span.Ref(0), ref span.Ref(pivot / 4));
                        Swap(ref span.Ref(pivot - 1), ref span.Ref(pivot - pivot / 4));

                        if (pivot > NintherThreshold)
                        {
                            Swap(ref span.Ref(1), ref span.Ref(pivot / 4 + 1));
                            Swap(ref span.Ref(2), ref span.Ref(pivot / 4 + 2));
                            Swap(ref span.Ref(pivot - 2), ref span.Ref(pivot - (pivot / 4 + 1)));
                            Swap(ref span.Ref(pivot - 3), ref span.Ref(pivot - (pivot / 4 + 2)));
                        }
                    }

                    if (rightSize >= InsertionSortThreshold)
                    {
                        Swap(ref span.Ref(pivot + 1), ref span.Ref(pivot + 1 + rightSize / 4));
                        Swap(ref span.Ref(size - 1), ref span.Ref(size - rightSize / 4));

                        if (rightSize > NintherThreshold)
                        {
                            Swap(ref span.Ref(pivot + 2), ref span.Ref(pivot + 2 + rightSize / 4));
                            Swap(ref span.Ref(pivot + 3), ref span.Ref(pivot + 3 + rightSize / 4));
                            Swap(ref span.Ref(size - 2), ref span.Ref(size - (1 + rightSize / 4)));
                            Swap(ref span.Ref(size - 3), ref span.Ref(size - (2 + rightSize / 4)));
                        }
                    }
                }
                else
                {
                    // If we were decently balanced and we tried to sort an already partitioned
                    // sequence try to use insertion sort.
                    if (hasPartitioned &&
                        PartialInsertionSort(ref span.Ref(0), pivot, comp) &&
                        PartialInsertionSort(ref span.Ref(pivot + 1), rightSize, comp))
                    {
                        return;
                    }
                }

                // Sort the left partition first using recursion and do tail recursion elimination for
                // the right-hand partition.
                SortLoop(span.Sub(0, pivot), comp, badAllowed, leftmost);
                span = span.Sub(pivot + 1, size);
                leftmost = false;
            }
        }

        [Template(nameof(T), nameof(comp), nameof(span))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Sort(Span<T> span, Comparison<T> comp)
        {
            SortLoop(span, comp, BitOperations.Log2((uint)span.Length));
        }
    }
}
