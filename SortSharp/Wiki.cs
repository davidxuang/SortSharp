using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SortSharp.Extensions;
using SortSharp.SourceGeneration;
using static SortSharp.Extensions.SpanExtensions;

namespace SortSharp;

public static partial class SpanExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WikiSort<T>(this Span<T> span, MemoryPolicy policy = MemoryPolicy.Fixed)
        => WikiSort<T, IComparer<T>?>(span, (IComparer<T>?)null, policy);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WikiSort<T>(this Span<T> span, IComparer<T>? comparer = null, MemoryPolicy policy = MemoryPolicy.Fixed)
        => WikiSort<T, IComparer<T>?>(span, comparer, policy);

    public static void WikiSort<T>(this Span<T> span, Comparison<T> compare, MemoryPolicy policy = MemoryPolicy.Fixed)
    {
        ArgumentNullException.ThrowIfNull(compare, nameof(compare));

        if (span.Length <= 1)
            return;
        Wiki.Fn<T>.Sort(span, compare, policy);
    }

    public static void WikiSort<T, TComparer>(this Span<T> span, TComparer comparer, MemoryPolicy policy = MemoryPolicy.Fixed)
        where TComparer : IComparer<T>?
    {
        if (span.Length <= 1)
            return;
        else if (typeof(TComparer).IsValueType)
#pragma warning disable CS8631
            Wiki.Cmp<T, TComparer>.Sort(span, comparer, policy);
#pragma warning restore CS8631
        else if (comparer is null || comparer as IComparer<T> == Comparer<T>.Default)
            Router<T>.To.WikiSort(span, policy);
        else
            Wiki.Cmp<T, IComparer<T>>.Sort(span, (IComparer<T>?)comparer ?? Comparer<T>.Default, policy);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WikiSort<K, V>(this Span<K> keys, Span<V> items, MemoryPolicy policy = MemoryPolicy.Fixed)
        => WikiSort<K, V, IComparer<K>?>(keys, items, null, policy);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WikiSort<K, V>(this Span<K> keys, Span<V> items, IComparer<K>? comparer = null, MemoryPolicy policy = MemoryPolicy.Fixed)
        => WikiSort<K, V, IComparer<K>?>(keys, items, comparer, policy);

    public static void WikiSort<K, V>(this Span<K> keys, Span<V> items, Comparison<K> compare, MemoryPolicy policy = MemoryPolicy.Fixed)
    {
        ArgumentOutOfRangeException.ThrowIf(keys.Length != items.Length, nameof(items));
        ArgumentNullException.ThrowIfNull(compare, nameof(compare));

        if (keys.Length <= 1)
            return;
        Wiki.Fn<K>.Sort(keys, items, compare, policy);
    }

    public static void WikiSort<K, V, TComparer>(this Span<K> keys, Span<V> items, TComparer comparer, MemoryPolicy policy = MemoryPolicy.Fixed)
        where TComparer : IComparer<K>?
    {
        ArgumentOutOfRangeException.ThrowIf(keys.Length != items.Length, nameof(items));

        if (keys.Length <= 1)
            return;
        else if (typeof(TComparer).IsValueType)
#pragma warning disable CS8631
            Wiki.Cmp<K, TComparer>.Sort(keys, items, comparer, policy);
#pragma warning restore CS8631
        else if (comparer is null || comparer as IComparer<K> == Comparer<K>.Default)
            Router<K>.To.WikiSort(keys, items, policy);
        else
            Wiki.Cmp<K, IComparer<K>>.Sort(keys, items, (IComparer<K>?)comparer ?? Comparer<K>.Default, policy);
    }
}

/// <see cref="https://github.com/BonzaiThePenguin/WikiSort"/>
[TemplateClass(Switch = TemplateVariants.IComparisonOperators)]
internal abstract partial class Wiki : SortBase
{
    private const int MaxStackallocCacheSize = 512;
    private const int MaxStackallocStructSize = 64; // restrict stack depth

    internal struct Iterator : IEnumerator<Range>
    {
        readonly int Size;
        readonly int Denominator;
        int Prev = 0;
        int Decimal = 0;
        int Numerator = 0;
        int DecimalStep;
        int NumeratorStep;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Iterator(int size, int minLevlel)
        {
            Size = size;
            uint PowerOf2 = 1u << BitOperations.Log2((uint)size);
            Denominator = (int)PowerOf2 / minLevlel;
            DecimalStep = size / Denominator;
            NumeratorStep = size % Denominator;
        }

        public readonly Range Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(Prev, Decimal);
        }
        readonly object IEnumerator.Current => Current;

        public readonly int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => DecimalStep;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (Decimal >= Size) return false;
            Prev = Decimal;
            Decimal += DecimalStep;
            Decimal += Math.DivRem(Numerator + NumeratorStep, Denominator, out Numerator);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset() => Numerator = Decimal = 0;

        public readonly void Dispose() { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveUp()
        {
            DecimalStep += DecimalStep;
            DecimalStep += Math.DivRem(NumeratorStep + NumeratorStep, Denominator, out NumeratorStep);
            return DecimalStep < Size;
        }
    }

    internal struct Pull
    {
        public int From;
        public int To;
        public int Count;
        public int Start;
        public int End;
    }

    internal sealed new partial class Fn<T> : SortBase.Fn<T>
    {
        [Template(nameof(T), nameof(comp))]
        internal static int FindFirstForward(ReadOnlySpan<T> span, int start, int end, ref readonly T value, Comparison<T> comp, int unique)
        {
            int size = end - start;
            if (size == 0) return start;

            int skip = Math.Max(size / unique, 1);
            int i;
            for (i = start + skip; Less(in span.Ref(i - 1), in value, comp); i += skip)
            {
                if (i >= end - skip)
                {
                    return i + LowerBound(span.Sub(i, end), in value, comp);
                }
            }
            return i - skip + LowerBound(span.Sub(i - skip, i), in value, comp);
        }

        [Template(nameof(T), nameof(comp))]
        internal static int FindLastForward(ReadOnlySpan<T> span, int start, int end, ref readonly T value, Comparison<T> comp, int unique)
        {
            int size = end - start;
            if (size == 0) return start;

            int skip = Math.Max(size / unique, 1);
            int i;
            for (i = start + skip; !Less(in value, in span.Ref(i - 1), comp); i += skip)
            {
                if (i >= end - skip)
                {
                    return i + UpperBound(span.Sub(i, end), in value, comp);
                }
            }
            return i - skip + UpperBound(span.Sub(i - skip, i), in value, comp);
        }

        [Template(nameof(T), nameof(comp))]
        internal static int FindFirstBackward(ReadOnlySpan<T> span, int start, int end, ref readonly T value, Comparison<T> comp, int unique)
        {
            int size = end - start;
            if (size == 0) return start;

            int skip = Math.Max(size / unique, 1);
            int i;
            for (i = end - skip; i > start && !Less(in span.Ref(i - 1), in value, comp); i -= skip)
            {
                if (i < start + skip)
                {
                    return start + LowerBound(span.Sub(start, i), in value, comp);
                }
            }
            return i + LowerBound(span.Sub(i, i + skip), in value, comp);
        }

        [Template(nameof(T), nameof(comp))]
        internal static int FindLastBackward(ReadOnlySpan<T> span, int start, int end, ref readonly T value, Comparison<T> comp, int unique)
        {
            int size = end - start;
            if (size == 0) return start;

            int skip = Math.Max(size / unique, 1);
            int i;
            for (i = end - skip; i > start && Less(in value, in span.Ref(i - 1), comp); i -= skip)
            {
                if (i < start + skip)
                {
                    return start + UpperBound(span.Sub(start, i), in value, comp);
                }
            }
            return i + UpperBound(span.Sub(i, i + skip), in value, comp);
        }

        // merge operation using an external buffer
        [Template(nameof(T), nameof(comp), nameof(span), nameof(cache))]
        static void MergeExternal(Span<T> span, int split, Span<T> cache, Comparison<T> comp)
        {
            // A fits into the cache, so use that instead of the internal buffer
            ref T indxA = ref cache.Ref(0);
            ref T lastA = ref cache.Ref(split);
            ref T indxB = ref span.Ref(split);
            ref T lastB = ref span.Ref(span.Length);
            ref T insert = ref span.Ref(0);

            if (split > 0 && split < span.Length)
            {
                while (true)
                {
                    if (!Less(in indxB, in indxA, comp))
                    {
                        insert = indxA;
                        indxA = ref Unsafe.Next(ref indxA);
                        insert = ref Unsafe.Next(ref insert);
                        if (Unsafe.AreSame(ref indxA, ref lastA)) break;
                    }
                    else
                    {
                        insert = indxB;
                        indxB = ref Unsafe.Next(ref indxB);
                        insert = ref Unsafe.Next(ref insert);
                        if (Unsafe.AreSame(ref indxB, ref lastB)) break;
                    }
                }
            }

            // copy the remainder of A into the final array
            int d = Unsafe.Offset(in indxA, in lastA), k = span.Offset(in insert);
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
            MemoryMarshal.CreateSpan(ref indxA, d).CopyTo(span.Sub(k, span.Length));
#else
            int i = cache.Offset(in indxA);
            cache.Slice(i, d).CopyTo(span.Slice(k, span.Length - k));
#endif
        }

        // merge operation using an internal buffer
        [Template(nameof(T), nameof(comp), nameof(span), nameof(buffer))]
        static void MergeInternal(Span<T> span, int split, Span<T> buffer, Comparison<T> comp)
        {
            // whenever we find a value to add to the final array, swap it with the value that's already in that spot
            // when this algorithm is finished, 'buffer' will contain its original contents, but in a different order
            ref T indxA = ref buffer.Ref(0);
            ref T lastA = ref buffer.Ref(split);
            ref T indxB = ref span.Ref(split);
            ref T lastB = ref span.Ref(span.Length);
            ref T insert = ref span.Ref(0);

            if (split > 0 && split < span.Length)
            {
                while (true)
                {
                    if (!Less(in indxB, in indxA, comp))
                    {
                        Swap(ref insert, ref indxA);
                        indxA = ref Unsafe.Next(ref indxA);
                        insert = ref Unsafe.Next(ref insert);
                        if (Unsafe.AreSame(ref indxA, ref lastA)) break;
                    }
                    else
                    {
                        Swap(ref insert, ref indxB);
                        indxB = ref Unsafe.Next(ref indxB);
                        insert = ref Unsafe.Next(ref insert);
                        if (Unsafe.AreSame(ref indxB, ref lastB)) break;
                    }
                }
            }

            // BlockSwap
            int d = Unsafe.Offset(in indxA, in lastA);
            SwapBlock(ref indxA, ref insert, d);
        }

        // merge operation without a buffer
        [Template(nameof(T), nameof(comp), nameof(span), nameof(cache))]
        static void MergeInPlace(Span<T> span, int split, Span<T> cache, Comparison<T> comp)
        {
            if (split == 0 || split == span.Length) return;

            /*
             this just repeatedly binary searches into B and rotates A into position.
             the paper suggests using the 'rotation-based Hwang and Lin algorithm' here,
             but I decided to stick with this because it had better situational performance

             (Hwang and Lin is designed for merging subarrays of very different sizes,
             but WikiSort almost always uses subarrays that are roughly the same size)

             normally this is incredibly suboptimal, but this function is only called
             when none of the A or B blocks in any subarray contained 2√A unique values,
             which places a hard limit on the number of times this will ACTUALLY need
             to binary search and rotate.

             according to my analysis the worst case is √A rotations performed on √A items
             once the constant factors are removed, which ends up being O(n)

             again, this is NOT a general-purpose solution – it only works well in this case!
             kind of like how the O(n^2) insertion sort is used in some places
             */

            int start = 0, end = span.Length;
            while (true)
            {
                // find the first place in B where the first item in A needs to be inserted
                int mid = split + LowerBound(span.Sub(split, end), in span.Ref(start), comp);

                // rotate A into place
                int amount = mid - split;
                Rotate(span.Sub(start, mid), split - start, cache);
                if (end == mid) break;

                // calculate the new A and B ranges
                split = mid;
                start += amount;
                start += UpperBound(span.Sub(start, split), in span.Ref(start), comp);
                if (start == split) break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Template(nameof(T), nameof(comp), nameof(span))]
        static void Sort2(Span<T> span, Span<byte> order, int i, int j, Comparison<T> comp)
        {
            if (Less(in span.Ref(j), in span.Ref(i), comp) ||
                (order.Ref(i) > order.Ref(j) && !Less(in span.Ref(i), in span.Ref(j), comp)))
            {
                Swap(ref span.Ref(i), ref span.Ref(j));
                Swap(ref order.Ref(i), ref order.Ref(j));
            }
        }

        // bottom-up merge sort combined with an in-place merge algorithm for O(1) memory use
        [Template(nameof(T), nameof(comp), nameof(span))]
        internal static unsafe void Sort(Span<T> span, Comparison<T> comp, MemoryPolicy policy)
        {
            int size = span.Length;

            // if the array is of size 0, 1, 2, or 3, just sort them like so:
            if (size < 4)
            {
                // hard-coded insertion sort
                if (size == 3)
                {
                    Sort2U(ref span.Ref(0), ref span.Ref(1), comp);
                    if (Less(in span.Ref(2), in span.Ref(1), comp))
                    {
                        Swap(ref span.Ref(2), ref span.Ref(1));
                        Sort2U(ref span.Ref(0), ref span.Ref(1), comp);
                    }
                }
                else if (size == 2)
                {
                    // swap the items if they're out of order
                    Sort2U(ref span.Ref(0), ref span.Ref(1), comp);
                }
                return;
            }

            // sort groups of 4-8 items at a time using an unstable sorting network,
            // but keep track of the original item orders to force it to be stable
            // http://pages.ripco.net/~jgamble/nw.html
            var iterator = new Iterator(size, 4);
            Span<byte> order = stackalloc byte[8];
            while (iterator.MoveNext())
            {
                order[0] = 0; order[1] = 1; order[2] = 2; order[3] = 3;
                order[4] = 4; order[5] = 5; order[6] = 6; order[7] = 7;
                var range = iterator.Current;
                Span<T> sub = span.Sub(range);

                switch (range.Length)
                {
                    case 8:
                        Sort2(sub, order, 0, 1, comp); Sort2(sub, order, 2, 3, comp); Sort2(sub, order, 4, 5, comp); Sort2(sub, order, 6, 7, comp);
                        Sort2(sub, order, 0, 2, comp); Sort2(sub, order, 1, 3, comp); Sort2(sub, order, 4, 6, comp); Sort2(sub, order, 5, 7, comp);
                        Sort2(sub, order, 1, 2, comp); Sort2(sub, order, 5, 6, comp); Sort2(sub, order, 0, 4, comp); Sort2(sub, order, 3, 7, comp);
                        Sort2(sub, order, 1, 5, comp); Sort2(sub, order, 2, 6, comp);
                        Sort2(sub, order, 1, 4, comp); Sort2(sub, order, 3, 6, comp);
                        Sort2(sub, order, 2, 4, comp); Sort2(sub, order, 3, 5, comp);
                        Sort2(sub, order, 3, 4, comp);
                        break;
                    case 7:
                        Sort2(sub, order, 1, 2, comp); Sort2(sub, order, 3, 4, comp); Sort2(sub, order, 5, 6, comp);
                        Sort2(sub, order, 0, 2, comp); Sort2(sub, order, 3, 5, comp); Sort2(sub, order, 4, 6, comp);
                        Sort2(sub, order, 0, 1, comp); Sort2(sub, order, 4, 5, comp); Sort2(sub, order, 2, 6, comp);
                        Sort2(sub, order, 0, 4, comp); Sort2(sub, order, 1, 5, comp);
                        Sort2(sub, order, 0, 3, comp); Sort2(sub, order, 2, 5, comp);
                        Sort2(sub, order, 1, 3, comp); Sort2(sub, order, 2, 4, comp);
                        Sort2(sub, order, 2, 3, comp);
                        break;
                    case 6:
                        Sort2(sub, order, 1, 2, comp); Sort2(sub, order, 4, 5, comp);
                        Sort2(sub, order, 0, 2, comp); Sort2(sub, order, 3, 5, comp);
                        Sort2(sub, order, 0, 1, comp); Sort2(sub, order, 3, 4, comp); Sort2(sub, order, 2, 5, comp);
                        Sort2(sub, order, 0, 3, comp); Sort2(sub, order, 1, 4, comp);
                        Sort2(sub, order, 2, 4, comp); Sort2(sub, order, 1, 3, comp);
                        Sort2(sub, order, 2, 3, comp);
                        break;
                    case 5:
                        Sort2(sub, order, 0, 1, comp); Sort2(sub, order, 3, 4, comp);
                        Sort2(sub, order, 2, 4, comp);
                        Sort2(sub, order, 2, 3, comp); Sort2(sub, order, 1, 4, comp);
                        Sort2(sub, order, 0, 3, comp);
                        Sort2(sub, order, 0, 2, comp); Sort2(sub, order, 1, 3, comp);
                        Sort2(sub, order, 1, 2, comp);
                        break;
                    case 4:
                        Sort2(sub, order, 0, 1, comp); Sort2(sub, order, 2, 3, comp);
                        Sort2(sub, order, 0, 2, comp); Sort2(sub, order, 1, 3, comp);
                        Sort2(sub, order, 1, 2, comp);
                        break;
                }
            }
            if (size < 8) return;

            // use a small cache to speed up some of the operations
            Span<T> cache;
            int cacheSize = (size + 1) / 2;
            IMemoryOwner<T>? owner = null;
            try
            {
                if (policy == MemoryPolicy.Balanced) cacheSize = (int)Math.Sqrt(cacheSize) + 1;

                if (policy < MemoryPolicy.Fixed)
                {
                    cache = Span<T>.Empty;
                    cacheSize = 0;
                }
                else if (policy == MemoryPolicy.Fixed || cacheSize < MaxStackallocCacheSize)
                {
                    cacheSize = Math.Min(size, MaxStackallocCacheSize);
                    cache = RuntimeHelpers.IsReferenceOrContainsReferences<T>() || Unsafe.SizeOf<T>() > MaxStackallocStructSize
                        ? (owner = MemoryPool<T>.Shared.Rent(cacheSize)).Memory.Span
                        : new Span<T>(
                            Unsafe.AsPointer(ref MemoryMarshal.GetReference(stackalloc byte[Unsafe.SizeOf<T>() * cacheSize])),
                            cacheSize);
                }
                else
                {
                    cache = (owner = MemoryPool<T>.Shared.Rent(cacheSize)).Memory.Span;
                    cacheSize = cache.Length;
                }

                // then merge sort the higher levels, which can be 8-15, 16-31, 32-63, 64-127, etc.
                var pull = (stackalloc Pull[2]);
                while (true)
                {
                    // if every A and B block will fit into the cache, use a special branch specifically for merging with the cache
                    // (we use < rather than <= since the block size might be one more than iterator.length())
                    if (iterator.Length < cacheSize)
                    {

                        // if four subarrays fit into the cache, it's faster to merge both pairs of subarrays into the cache,
                        // then merge the two merged subarrays from the cache back into the original array
                        if ((iterator.Length + 1) * 4 <= cacheSize && iterator.Length * 4 <= size)
                        {
                            iterator.Reset();
                            while (iterator.MoveNext())
                            {
                                var A1 = iterator.Current; iterator.MoveNext();
                                var B1 = iterator.Current; iterator.MoveNext();
                                var A2 = iterator.Current; iterator.MoveNext();
                                var B2 = iterator.Current;

                                if (Less(in span.Ref(B1.End - 1), in span.Ref(A1.Start), comp))
                                {
                                    // the two ranges are in reverse order, so copy them in reverse order into the cache
                                    span.Sub(A1).CopyTo(cache.Sub(B1.Length, cacheSize));
                                    span.Sub(B1).CopyTo(cache);
                                }
                                else if (Less(in span.Ref(B1.Start), in span.Ref(A1.End - 1), comp))
                                {
                                    // these two ranges weren't already in order, so merge them into the cache
                                    Debug.Assert(A1.End == B1.Start);
                                    Merge(span.Sub(A1.Start, B1.End), A1.Length, cache, comp);
                                    //MergeBidirectional(ref span.Ref(A1.Start), ref cache.Ref(0), A1.Length, B1.End - A1.Start, comp);
                                }
                                else
                                {
                                    // if A1, B1, A2, and B2 are all in order, skip doing anything else
                                    if (!Less(in span.Ref(B2.Start), in span.Ref(A2.End - 1), comp) &&
                                        !Less(in span.Ref(A2.Start), in span.Ref(B1.End - 1), comp))
                                        continue;

                                    // copy A1 and B1 into the cache in the same order at once
                                    span.Sub(A1.Start, B1.End).CopyTo(cache);
                                }
                                A1.End = B1.End;

                                var A3 = new Range(0, A1.Length);
                                var B3 = new Range(A1.Length, B2.End - A1.Start);

                                // merge A2 and B2 into the cache
                                if (Less(in span.Ref(B2.End - 1), in span.Ref(A2.Start), comp))
                                {
                                    // the two ranges are in reverse order, so copy them in reverse order into the cache
                                    span.Sub(A2).CopyTo(cache.Sub(B3.Start + B2.Length, B3.End));
                                    span.Sub(B2).CopyTo(cache.Sub(B3));
                                }
                                else if (Less(in span.Ref(B2.Start), in span.Ref(A2.End - 1), comp))
                                {
                                    // these two ranges weren't already in order, so merge them back into the array
                                    Debug.Assert(A2.End == B2.Start);
                                    Merge(span.Sub(A2.Start, B2.End), A2.Length, cache.Sub(B3), comp);
                                    //MergeBidirectional(ref span.Ref(A2.Start), ref cache.Ref(B3.Start), A2.Length, B2.End - A2.Start, comp);
                                }
                                else
                                {
                                    // copy A2 and B2 into the cache in the same order at once
                                    span.Sub(A2.Start, B2.End).CopyTo(cache.Sub(B3));
                                }
                                A2.End = B2.End;

                                if (Less(in cache.Ref(B3.End - 1), in cache.Ref(0), comp))
                                {
                                    // the two ranges are in reverse order, so copy them in reverse order into the array
                                    cache.Sub(A3).CopyTo(span.Sub(A1.Start + A2.Length, A2.End));
                                    cache.Sub(B3).CopyTo(span.Sub(A1.Start, A2.End));
                                }
                                else if (Less(in cache.Ref(B3.Start), in cache.Ref(A3.End - 1), comp))
                                {
                                    // these two ranges weren't already in order, so merge them back into the array
                                    Debug.Assert(A3.End == B3.Start);
                                    Merge(cache.Sub(A3.Start, B3.End), A3.Length, span.Sub(A1.Start, A2.End), comp);
                                    //MergeBidirectional(ref cache.Ref(A3.Start), ref span.Ref(A1.Start), A3.Length, B3.End - A3.Start, comp);
                                }
                                else
                                {
                                    // copy the two ranges back into the array in the same order at once
                                    cache.Sub(0, B3.End).CopyTo(span.Sub(A1.Start, A2.End));
                                }
                            }

                            // we merged two levels at the same time, so we're done with this level already
                            // (iterator.nextLevel() is called again at the bottom of this outer merge loop)
                            iterator.MoveUp();
                        }
                        else
                        {
                            iterator.Reset();
                            while (iterator.MoveNext())
                            {
                                var A = iterator.Current; iterator.MoveNext();
                                var B = iterator.Current;

                                if (Less(in span.Ref(B.End - 1), in span.Ref(A.Start), comp))
                                {
                                    // the two ranges are in reverse order, so a simple rotation should fix it
                                    Rotate(span.Sub(A.Start, B.End), A.Length, cache);
                                }
                                else if (Less(in span.Ref(B.Start), in span.Ref(A.End - 1), comp))
                                {
                                    // these two ranges weren't already in order, so we'll need to merge them!
                                    span.Sub(A).CopyTo(cache);
                                    MergeExternal(span.Sub(A.Start, B.End), A.Length, cache, comp);
                                }
                            }
                        }
                    }
                    else
                    {
                        // this is where the in-place merge logic starts!
                        // 1. pull out two internal buffers each containing √A unique values
                        //     1a. adjust block_size and buffer_size if we couldn't find enough unique values
                        // 2. loop over the A and B subarrays within this level of the merge sort
                        //     3. break A and B into blocks of size 'block_size'
                        //     4. "tag" each of the A blocks with values from the first internal buffer
                        //     5. roll the A blocks through the B blocks and drop/rotate them where they belong
                        //     6. merge each A block with any B values that follow, using the cache or the second internal buffer
                        // 7. sort the second internal buffer if it exists
                        // 8. redistribute the two internal buffers back into the array
                        int blockSize = (int)Math.Sqrt(iterator.Length);
                        int bufferSize = iterator.Length / blockSize + 1;

                        // as an optimization, we really only need to pull out the internal buffers once for each level of merges
                        // after that we can reuse the same buffers over and over, then redistribute it when we're finished with this level
                        Range buffer1 = new(0, 0);
                        Range buffer2 = new(0, 0);
                        int index, last;
                        int count, p = 0;
                        pull.Clear();

                        // find two internal buffers of size 'buffer_size' each
                        // let's try finding both buffers at the same time from a single A or B subarray
                        int find = bufferSize + bufferSize;
                        bool findSeparately = false;

                        if (blockSize <= cacheSize)
                        {
                            // if every A block fits into the cache then we won't need the second internal buffer,
                            // so we really only need to find 'buffer_size' unique values
                            find = bufferSize;
                        }
                        else if (find > iterator.Length)
                        {
                            // we can't fit both buffers into the same A or B subarray, so find two buffers separately
                            find = bufferSize;
                            findSeparately = true;
                        }

                        // we need to find either a single contiguous space containing 2√A unique values (which will be split up into two buffers of size √A each),
                        // or we need to find one buffer of < 2√A unique values, and a second buffer of √A unique values,
                        // OR if we couldn't find that many unique values, we need the largest possible buffer we can get

                        // in the case where it couldn't find a single buffer of at least √A unique values,
                        // all of the Merge steps must be replaced by a different merge algorithm (MergeInPlace)
                        iterator.Reset();
                        while (iterator.MoveNext())
                        {
                            var A = iterator.Current; iterator.MoveNext();
                            var B = iterator.Current;

                            // check A for the number of unique values we need to fill an internal buffer
                            // these values will be pulled out to the start of A
                            for (last = A.Start, count = 1;
                                count < find;
                                last = index, ++count)
                            {
                                index = FindLastForward(span, last + 1, A.End, in span.Ref(last), comp, find - count);
                                if (index == A.End) break;
                                Debug.Assert(index < A.End);
                            }
                            index = last;

                            if (count >= bufferSize)
                            {
                                // just store information about where the values will be pulled from and to,
                                // as well as how many values there are, to create the two internal buffers
                                // PULL(_to);

                                // keep track of the range within the array where we'll need to "pull out" these values to create the internal buffer
                                pull[p].Start = A.Start;
                                pull[p].End = B.End;
                                pull[p].Count = count;
                                pull[p].From = index;
                                pull[p].To = A.Start;

                                p = 1;

                                if (count == bufferSize + bufferSize)
                                {
                                    // we were able to find a single contiguous section containing 2√A unique values,
                                    // so this section can be used to contain both of the internal buffers we'll need
                                    buffer1 = new(A.Start, A.Start + bufferSize);
                                    buffer2 = new(A.Start + bufferSize, A.Start + count);
                                    break;
                                }
                                else if (find == bufferSize + bufferSize)
                                {
                                    // we found a buffer that contains at least √A unique values, but did not contain the full 2√A unique values,
                                    // so we still need to find a second separate buffer of at least √A unique values
                                    buffer1 = new(A.Start, A.Start + count);
                                    find = bufferSize;
                                }
                                else if (blockSize <= cacheSize)
                                {
                                    // we found the first and only internal buffer that we need, so we're done!
                                    buffer1 = new(A.Start, A.Start + count);
                                    break;
                                }
                                else if (findSeparately)
                                {
                                    // found one buffer, but now find the other one
                                    buffer1 = new(A.Start, A.Start + count);
                                    findSeparately = false;
                                }
                                else
                                {
                                    // we found a second buffer in an 'A' subarray containing √A unique values, so we're done!
                                    buffer2 = new(A.Start, A.Start + count);
                                    break;
                                }
                            }
                            else if (p == 0 && count > buffer1.Length)
                            {
                                // keep track of the largest buffer we were able to find
                                buffer1 = new(A.Start, A.Start + count);
                                pull[p].Start = A.Start;
                                pull[p].End = B.End;
                                pull[p].Count = count;
                                pull[p].From = index;
                                pull[p].To = A.Start;
                            }

                            // check B for the number of unique values we need to fill an internal buffer
                            // these values will be pulled out to the end of B
                            for (last = B.End - 1, count = 1;
                                count < find;
                                last = index - 1, ++count)
                            {
                                index = FindFirstBackward(span, B.Start, last, in span.Ref(last), comp, find - count);
                                if (index == B.Start) break;
                                Debug.Assert(index > B.Start);
                            }
                            index = last;

                            if (count >= bufferSize)
                            {
                                // keep track of the range within the array where we'll need to "pull out" these values to create the internal buffer
                                pull[p].Start = A.Start;
                                pull[p].End = B.End;
                                pull[p].Count = count;
                                pull[p].From = index;
                                pull[p].To = B.End;

                                p = 1;

                                if (count == bufferSize + bufferSize)
                                {
                                    // we were able to find a single contiguous section containing 2√A unique values,
                                    // so this section can be used to contain both of the internal buffers we'll need
                                    buffer1 = new(B.End - count, B.End - bufferSize);
                                    buffer2 = new(B.End - bufferSize, B.End);
                                    break;
                                }
                                else if (find == bufferSize + bufferSize)
                                {
                                    // we found a buffer that contains at least √A unique values, but did not contain the full 2√A unique values,
                                    // so we still need to find a second separate buffer of at least √A unique values
                                    buffer1 = new(B.End - count, B.End);
                                    find = bufferSize;
                                }
                                else if (blockSize <= cacheSize)
                                {
                                    // we found the first and only internal buffer that we need, so we're done!
                                    buffer1 = new(B.End - count, B.End);
                                    break;
                                }
                                else if (findSeparately)
                                {
                                    // found one buffer, but now find the other one
                                    buffer1 = new(B.End - count, B.End);
                                    findSeparately = false;
                                }
                                else
                                {
                                    // buffer2 will be pulled out from a 'B' subarray, so if the first buffer was pulled out from the corresponding 'A' subarray,
                                    // we need to adjust the end point for that A subarray so it knows to stop redistributing its values before reaching buffer2
                                    if (pull[0].Start == A.Start)
                                    {
                                        pull[0].End -= pull[1].Count;
                                    }

                                    // we found a second buffer in a 'B' subarray containing √A unique values, so we're done!
                                    buffer2 = new(B.End - count, B.End);
                                    break;
                                }
                            }
                            else if (p == 0 && count > buffer1.Length)
                            {
                                // keep track of the largest buffer we were able to find
                                buffer1 = new(B.End - count, B.End);
                                pull[p].Start = A.Start;
                                pull[p].End = B.End;
                                pull[p].Count = count;
                                pull[p].From = index;
                                pull[p].To = B.End;
                            }
                        }

                        // pull out the two ranges so we can use them as internal buffers
                        for (p = 0; p < 2; ++p)
                        {
                            int length = pull[p].Count;

                            if (pull[p].To < pull[p].From)
                            {
                                // we're pulling the values out to the left, which means the start of an A subarray
                                index = pull[p].From;
                                for (count = 1; count < length; ++count)
                                {
                                    index = FindFirstBackward(span, pull[p].To, pull[p].From - (count - 1),
                                        in span.Ref(index - 1), comp, length - count);
                                    int end = pull[p].From + 1;
                                    Rotate(span.Sub(index + 1, end), end - count - (index + 1), cache);
                                    pull[p].From = index + count;
                                }
                            }
                            else if (pull[p].To > pull[p].From)
                            {
                                // we're pulling values out to the right, which means the end of a B subarray
                                index = pull[p].From + 1;
                                for (count = 1; count < length; ++count)
                                {
                                    index = FindLastForward(span, index, pull[p].To,
                                        in span.Ref(index), comp, length - count);
                                    int start = pull[p].From;
                                    Rotate(span.Sub(start, index - 1), count, cache);
                                    pull[p].From = index - count - 1;
                                }
                            }
                        }

                        // adjust block_size and buffer_size based on the values we were able to pull out
                        bufferSize = buffer1.Length;
                        blockSize = iterator.Length / bufferSize + 1;

                        // the first buffer NEEDS to be large enough to tag each of the evenly sized A blocks,
                        // so this was originally here to test the math for adjusting block_size above
                        //assert((iterator.length() + 1)/block_size <= buffer_size);

                        // now that the two internal buffers have been created, it's time to merge each A+B combination at this level of the merge sort!
                        iterator.Reset();
                        while (iterator.MoveNext())
                        {
                            var A = iterator.Current; iterator.MoveNext();
                            var B = iterator.Current;

                            // remove any parts of A or B that are being used by the internal buffers
                            int start = A.Start;
                            if (start == pull[0].Start)
                            {
                                if (pull[0].From > pull[0].To)
                                {
                                    A.Start += pull[0].Count;

                                    // if the internal buffer takes up the entire A or B subarray, then there's nothing to merge
                                    // this only happens for very small subarrays, like √4 = 2, 2 * (2 internal buffers) = 4,
                                    // which also only happens when cache_size is small or 0 since it'd otherwise use MergeExternal
                                    if (A.Length == 0) continue;
                                }
                                else if (pull[0].From < pull[0].To)
                                {
                                    B.End -= pull[0].Count;
                                    if (B.Length == 0) continue;
                                }
                            }
                            if (start == pull[1].Start)
                            {
                                if (pull[1].From > pull[1].To)
                                {
                                    A.Start += pull[1].Count;
                                    if (A.Length == 0) continue;
                                }
                                else if (pull[1].From < pull[1].To)
                                {
                                    B.End -= pull[1].Count;
                                    if (B.Length == 0) continue;
                                }
                            }

                            if (Less(in span.Ref(B.End - 1), in span.Ref(A.Start), comp))
                            {
                                // the two ranges are in reverse order, so a simple rotation should fix it
                                Rotate(span.Sub(A.Start, B.End), A.Length, cache);
                            }
                            else if (Less(in span.Ref(B.Start), in span.Ref(A.End - 1), comp))
                            {
                                // these two ranges weren't already in order, so we'll need to merge them!

                                // break the remainder of A into blocks. firstA is the uneven-sized first A block
                                Range lastA = new(A.Start, A.Start + A.Length % blockSize); // aliased for firstA
                                Range blockA = new(lastA.End, A.End);

                                // swap the first value of each A block with the values in buffer1
                                ref T indexA = ref span.Ref(buffer1.Start);
                                for (index = lastA.End;
                                    index < blockA.End;
                                    indexA = ref Unsafe.Next(ref indexA), index += blockSize)
                                {
                                    Swap(ref indexA, ref span.Ref(index));
                                }

                                // start rolling the A blocks through the B blocks!
                                // when we leave an A block behind we'll need to merge the previous A block with any B blocks that follow it, so track that information as well
                                Range lastB = new(0, 0);
                                Range blockB = new(B.Start, Math.Min(B.Start + blockSize, B.End));
                                indexA = ref span.Ref(buffer1.Start);

                                // if the first unevenly sized A block fits into the cache, copy it there for when we go to Merge it
                                // otherwise, if the second buffer is available, block swap the contents into that
                                if (lastA.Length <= cacheSize)
                                    span.Sub(lastA).CopyTo(cache);
                                else if (buffer2.Length > 0)
                                    SwapBlock(ref span.Ref(lastA.Start), ref span.Ref(buffer2.Start), lastA.Length);

                                if (blockA.Length > 0)
                                {
                                    while (true)
                                    {
                                        // if there's a previous B block and the first value of the minimum A block is <= the last value of the previous B block,
                                        // then drop that minimum A block behind. or if there are no B blocks left then keep dropping the remaining A blocks.
                                        if ((lastB.Length > 0 && !Less(in span.Ref(lastB.End - 1), in indexA, comp)) ||
                                            blockB.Length == 0)
                                        {
                                            // figure out where to split the previous B block, and rotate it at the split
                                            int bSplit = lastB.Start + LowerBound(span.Sub(lastB), in indexA, comp);
                                            int bRemaining = lastB.End - bSplit;

                                            // swap the minimum A block to the beginning of the rolling A blocks
                                            int minA = blockA.Start;
                                            for (int findA = minA + blockSize; findA < blockA.End; findA += blockSize)
                                            {
                                                if (Less(in span.Ref(findA), in span.Ref(minA), comp))
                                                    minA = findA;
                                            }
                                            SwapBlock(ref span.Ref(blockA.Start), ref span.Ref(minA), blockSize);

                                            // swap the first item of the previous A block back with its original value, which is stored in buffer1
                                            Swap(ref span.Ref(blockA.Start), ref indexA);
                                            indexA = ref Unsafe.Next(ref indexA);

                                            // locally merge the previous A block with the B values that follow it
                                            // if lastA fits into the external cache we'll use that (with MergeExternal),
                                            // or if the second internal buffer exists we'll use that (with MergeInternal),
                                            // or failing that we'll use a strictly in-place merge algorithm (MergeInPlace)
                                            if (lastA.Length <= cacheSize)
                                                MergeExternal(span.Sub(lastA.Start, bSplit), lastA.Length, cache, comp);
                                            else if (buffer2.Length > 0)
                                                MergeInternal(span.Sub(lastA.Start, bSplit), lastA.Length, span.Sub(buffer2), comp);
                                            else
                                                MergeInPlace(span.Sub(lastA.Start, bSplit), lastA.Length, cache, comp);

                                            if (buffer2.Length > 0 || blockSize <= cacheSize)
                                            {
                                                // copy the previous A block into the cache or buffer2, since that's where we need it to be when we go to merge it anyway
                                                if (blockSize <= cacheSize)
                                                    span.Sub(blockA.Start, blockA.Start + blockSize).CopyTo(cache);
                                                else
                                                    SwapBlock(ref span.Ref(blockA.Start), ref span.Ref(buffer2.Start), blockSize);

                                                // this is equivalent to rotating, but faster
                                                // the area normally taken up by the A block is either the contents of buffer2, or data we don't need anymore since we memcopied it
                                                // either way we don't need to retain the order of those items, so instead of rotating we can just block swap B to where it belongs
                                                SwapBlock(ref span.Ref(bSplit), ref span.Ref(blockA.Start + blockSize - bRemaining), bRemaining);
                                            }
                                            else
                                            {
                                                // we are unable to use the 'buffer2' trick to speed up the rotation operation since buffer2 doesn't exist, so perform a normal rotation
                                                Rotate(span.Sub(bSplit, blockA.Start + blockSize), blockA.Start - bSplit, cache);
                                            }

                                            // update the range for the remaining A blocks, and the range remaining from the B block after it was split
                                            lastA = new(blockA.Start - bRemaining, blockA.Start - bRemaining + blockSize);
                                            lastB = new(lastA.End, lastA.End + bRemaining);

                                            // if there are no more A blocks remaining, this step is finished!
                                            blockA.Start += blockSize;
                                            if (blockA.Length == 0) break;
                                        }
                                        else if (blockB.Length < blockSize)
                                        {
                                            // move the last B block, which is unevenly sized, to before the remaining A blocks, by using a rotation
                                            Rotate(span.Sub(blockA.Start, blockB.End), blockB.Start - blockA.Start); // cache occupied

                                            lastB = new(blockA.Start, blockA.Start + blockB.Length);
                                            blockA.Start += blockB.Length;
                                            blockA.End += blockB.Length;
                                            blockB.End = blockB.Start;
                                        }
                                        else
                                        {
                                            // roll the leftmost A block to the end by swapping it with the next B block
                                            SwapBlock(ref span.Ref(blockA.Start), ref span.Ref(blockB.Start), blockSize);
                                            lastB = new(blockA.Start, blockA.Start + blockSize);

                                            blockA.Start += blockSize;
                                            blockA.End += blockSize;
                                            blockB.Start += blockSize;

                                            if (blockB.End > B.End - blockSize)
                                            {
                                                blockB.End = B.End;
                                            }
                                            else
                                            {
                                                blockB.End += blockSize;
                                            }
                                        }
                                    }
                                }

                                // merge the last A block with the remaining B values
                                if (lastA.Length <= cacheSize)
                                    MergeExternal(span.Sub(lastA.Start, B.End), lastA.Length, cache, comp);
                                else if (buffer2.Length > 0)
                                    MergeInternal(span.Sub(lastA.Start, B.End), lastA.Length, span.Sub(buffer2), comp);
                                else
                                    MergeInPlace(span.Sub(lastA.Start, B.End), lastA.Length, cache, comp);
                            }
                        }

                        // when we're finished with this merge step we should have the one or two internal buffers left over, where the second buffer is all jumbled up
                        // insertion sort the second buffer, then redistribute the buffers back into the array using the opposite process used for creating the buffer

                        // while an unstable sort like std::sort could be applied here, in benchmarks it was consistently slightly slower than a simple insertion sort,
                        // even for tens of millions of items. this may be because insertion sort is quite fast when the data is already somewhat sorted, like it is here
                        InsertionSort(ref span.Ref(buffer2.Start), buffer2.Length, comp);

                        for (p = 0; p < 2; ++p)
                        {
                            int unique = pull[p].Count * 2;
                            if (pull[p].From > pull[p].To)
                            {
                                // the values were pulled out to the left, so redistribute them back to the right
                                Range buffer = new(pull[p].Start, pull[p].Start + pull[p].Count);
                                while (buffer.Length > 0)
                                {
                                    index = FindFirstForward(span, buffer.End, pull[p].End, in span.Ref(buffer.Start), comp, unique);
                                    int amount = index - buffer.End;
                                    Rotate(span.Sub(buffer.Start, index), buffer.Length, cache);
                                    buffer.Start += (amount + 1);
                                    buffer.End += amount;
                                    unique -= 2;
                                }
                            }
                            else if (pull[p].From < pull[p].To)
                            {
                                // the values were pulled out to the right, so redistribute them back to the left
                                Range buffer = new(pull[p].End - pull[p].Count, pull[p].End);
                                while (buffer.Length > 0)
                                {
                                    index = FindLastBackward(span, pull[p].Start, buffer.Start, in span.Ref(buffer.End - 1), comp, unique);
                                    int amount = buffer.Start - index;
                                    Rotate(span.Sub(index, buffer.End), buffer.Start - index, cache);
                                    buffer.Start -= amount;
                                    buffer.End -= (amount + 1);
                                    unique -= 2;
                                }
                            }
                        }
                    }

                    // double the size of each A and B subarray that will be merged in the next level
                    if (!iterator.MoveUp()) break;
                }
            }
            finally
            {
                owner?.Dispose();
            }
        }
    }
}
