using System;
using System.Buffers;
using System.Collections.Generic;
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
    /// <inheritdoc cref="GrailSort{T, TComparer}(Span{T}, TComparer, MemoryProfile)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GrailSort<T>(this Span<T> span, MemoryProfile profile = MemoryProfile.Baseline)
        => GrailSort<T, IComparer<T>?>(span, (IComparer<T>?)null, profile);

    /// <inheritdoc cref="GrailSort{T, TComparer}(Span{T}, TComparer, MemoryProfile)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GrailSort<T>(this Span<T> span, IComparer<T>? comparer = null, MemoryProfile profile = MemoryProfile.Baseline)
        => GrailSort<T, IComparer<T>?>(span, comparer, profile);

    /// <inheritdoc cref="GrailSort{T, TComparer}(Span{T}, TComparer, MemoryProfile)" />
    public static void GrailSort<T>(this Span<T> span, Comparison<T> comparer, MemoryProfile profile = MemoryProfile.Baseline)
    {
        ArgumentNullException.ThrowIfNull(comparer, nameof(comparer));

        if (span.Length <= 1)
            return;
        Grail.Fn<T>.Sort(span, comparer, profile);
    }

    /// <summary>
    /// Sorts the elements in the span using the <see href="https://github.com/HolyGrailSortProject/Rewritten-Grailsort">GrailSort</see> algorithm,
    /// which is a <see href="https://en.wikipedia.org/wiki/Sorting_algorithm#Stability">stable</see> block merge sort algorithm.
    /// </summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <typeparam name="TComparer">The type of the comparer.</typeparam>
    /// <param name="span">The span to sort.</param>
    /// <param name="comparer">The comparer to use for sorting.</param>
    /// <param name="profile">The profile to use for allocating temporary cache. A fixed limit is applied with the default profile.</param>
    public static void GrailSort<T, TComparer>(this Span<T> span, TComparer comparer, MemoryProfile profile = MemoryProfile.Baseline)
        where TComparer : IComparer<T>?
    {
        if (span.Length <= 1)
            return;
        else if (typeof(TComparer).IsValueType)
#pragma warning disable CS8631
            Grail.Cmp<T, TComparer>.Sort(span, comparer, profile);
#pragma warning restore CS8631
        else if (comparer is null || comparer as IComparer<T> == Comparer<T>.Default)
            Dispatcher<T>.To.GrailSort(span, profile);
        else
            Grail.Cmp<T, IComparer<T>>.Sort(span, (IComparer<T>?)comparer ?? Comparer<T>.Default, profile);
    }

    /// <inheritdoc cref="GrailSort{K, V, TComparer}(Span{K}, Span{V}, TComparer, MemoryProfile)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GrailSort<K, V>(this Span<K> keys, Span<V> items, MemoryProfile profile = MemoryProfile.Baseline)
        => GrailSort<K, V, IComparer<K>?>(keys, items, null, profile);

    /// <inheritdoc cref="GrailSort{K, V, TComparer}(Span{K}, Span{V}, TComparer, MemoryProfile)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GrailSort<K, V>(this Span<K> keys, Span<V> items, IComparer<K>? comparer = null, MemoryProfile profile = MemoryProfile.Baseline)
        => GrailSort<K, V, IComparer<K>?>(keys, items, comparer, profile);

    /// <inheritdoc cref="GrailSort{K, V, TComparer}(Span{K}, Span{V}, TComparer, MemoryProfile)" />
    public static void GrailSort<K, V>(this Span<K> keys, Span<V> items, Comparison<K> comparer, MemoryProfile profile = MemoryProfile.Baseline)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(keys.Length, items.Length, nameof(items));
        ArgumentNullException.ThrowIfNull(comparer, nameof(comparer));

        if (keys.Length <= 1)
            return;
        Grail.Fn<K>.Sort(keys, items, comparer, profile);
    }

#pragma warning disable CS1573
    /// <typeparam name="K">The type of the keys.</typeparam>
    /// <typeparam name="V">The type of the values.</typeparam>
    /// <typeparam name="TComparer">The type of the comparer for the keys.</typeparam>
    /// <param name="keys">The span of keys to sort.</param>
    /// <param name="items">The span of values to sort.</param>
    /// <param name="comparer">The comparer to use for sorting the keys.</param>
    /// <inheritdoc cref="GrailSort{T, TComparer}(Span{T}, TComparer, MemoryProfile)" />
    public static void GrailSort<K, V, TComparer>(this Span<K> keys, Span<V> items, TComparer comparer, MemoryProfile profile = MemoryProfile.Baseline)
        where TComparer : IComparer<K>?
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(keys.Length, items.Length, nameof(items));

        if (keys.Length <= 1)
            return;
        else if (typeof(TComparer).IsValueType)
#pragma warning disable CS8631
            Grail.Cmp<K, TComparer>.Sort(keys, items, comparer, profile);
#pragma warning restore CS8631
        else if (comparer is null || comparer as IComparer<K> == Comparer<K>.Default)
            Dispatcher<K>.To.GrailSort(keys, items, profile);
        else
            Grail.Cmp<K, IComparer<K>>.Sort(keys, items, (IComparer<K>?)comparer ?? Comparer<K>.Default, profile);
    }
}
#pragma warning restore CS1573

[Sort(Properties = SortProperties.Stable, Disable = DefaultOverloads.IComparisonOperators)]
internal static partial class Grail
{
    private const int StaticExtBufferLen = 512;

    private enum Subarray : sbyte
    {
        Left = -1,
        Right = 1,
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Subarray Flip(Subarray subArray) => (Subarray)(-(sbyte)subArray);

    private ref struct Context<T>
    {
        public int CurrBlockLen;
        public Subarray CurrBlockOrigin;
        public bool IsExtBufferIdeal;
        public Span<T> ExtBuffer;

        [OverloadTemplate(nameof(T), null, nameof(buffer), Disable = DefaultOverloads.KeyValue)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Context<T> FromBuffer(Span<T> buffer) => new()
        {
            ExtBuffer = buffer,
        };

        [OverloadTemplate(nameof(T), null, nameof(buffer), Disable = DefaultOverloads.KeyValue)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Destruct(out Span<T> buffer) => buffer = ExtBuffer;
    }

    private ref struct Context<T, V>
    {
        public int CurrBlockLen;
        public Subarray CurrBlockOrigin;
        public bool IsExtBufferIdeal;
        public Span<T> ExtBuffer;
        public Span<V> ExtBufferV;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Context<T, V> FromBuffer(Span<T> buffer, Span<V> bufferV) => new()
        {
            ExtBuffer = buffer,
            ExtBufferV = bufferV,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Destruct(out Span<T> buffer, out Span<V> bufferV)
        {
            buffer = ExtBuffer;
            bufferV = ExtBufferV;
        }
    }

    internal sealed partial class Fn<T> : ComparisonOperations.Fn<T>
    {
        // cost: 2 * length + idealKeys^2 / 2
        [OverloadTemplate(nameof(T), nameof(comp), nameof(span))]
        static int CollectKeys(Span<T> span, int idealKeys, ref readonly Context<T> context, Comparison<T> comp)
        {
            Debug.Assert(span.Length > 0 && idealKeys > 0);

            int keysFound = 1; // by itself, the first item in the array is our first unique key
            int firstKey = 0; // the first item in the array is at the first position in the array
            int currKey = 1; // the index used for finding potentially unique items ("keys") in the array

            context.Destruct(out Span<T> extBuffer);
            while (currKey < span.Length && keysFound < idealKeys)
            {
                // Find the location in the key-buffer where our current key can be inserted in sorted order.
                // If the key at insertPos is equal to currKey, then currKey isn't unique and we move on.
                int insertPos = LowerBound(span.Sub(firstKey, firstKey + keysFound), in span.Ref(currKey), comp);

                // The second part of this conditional does the equal check we were just talking about; however,
                // if currKey is larger than everything in the key-buffer (meaning insertPos == keysFound),
                // then that also tells us it wasn't *equal* to anything in the key-buffer. Magic! :) 
                if (insertPos == keysFound ||
                    Compare(in span.Ref(currKey), in span.Ref(firstKey + insertPos), comp) != 0)
                {
                    // First, rotate the key-buffer over to currKey's immediate left...
                    // (this helps save a TON of swaps/writes!!!)
                    Rotate(span.Sub(firstKey, currKey), keysFound, extBuffer);

                    // Update the new position of firstKey...
                    firstKey = currKey - keysFound;

                    // Then, "insertion sort" currKey to its spot in the key-buffer!
                    Rotate(span.Sub(firstKey + insertPos, firstKey + keysFound + 1), keysFound - insertPos, extBuffer);

                    // One step closer to idealKeys.
                    keysFound++;
                }
                // Move on and test the next key...
                currKey++;
            }

            // Bring however many keys we found back to the beginning of our array,
            // and return the number of keys collected.
            Rotate(span.Sub(0, firstKey + keysFound), firstKey, extBuffer);
            return keysFound;
        }

        [OverloadTemplate(nameof(T), nameof(comp), nameof(span))]
        static void PairwiseSwaps(Span<T> span, Comparison<T> comp)
        {
            ref T left = ref span.Ref(2);
            ref T right = ref span.Ref(3);
            ref T last = ref span.Ref(span.Length);
            for (; Unsafe.IsAddressLessThan(in right, in last); right = ref Unsafe.Add(ref right, 2))
            {
                left = ref Unsafe.Dec(ref right);

                if (Less(in right, in left, comp))
                {
                    Swap(ref Unsafe.Subtract(ref left, 2), ref right);
                    Swap(ref Unsafe.Subtract(ref right, 2), ref left);
                }
                else
                {
                    Swap(ref Unsafe.Subtract(ref left, 2), ref left);
                    Swap(ref Unsafe.Subtract(ref right, 2), ref right);
                }
            }

            left = ref Unsafe.Dec(ref right);
            if (Unsafe.IsAddressLessThan(ref left, ref last))
            {
                Swap(ref Unsafe.Subtract(ref left, 2), ref left);
            }
        }

        [OverloadTemplate(nameof(T), nameof(comp), nameof(span))]
        static void PairwiseWrites(Span<T> span, Comparison<T> comp)
        {
            ref T left = ref span.Ref(2);
            ref T right = ref span.Ref(3);
            ref T last = ref span.Ref(span.Length);
            for (; Unsafe.IsAddressLessThan(in right, in last); right = ref Unsafe.Add(ref right, 2))
            {
                left = ref Unsafe.Dec(ref right);

                if (Less(in right, in left, comp))
                {
                    Unsafe.Subtract(ref left, 2) = right;
                    Unsafe.Subtract(ref right, 2) = left;
                }
                else
                {
                    Unsafe.Subtract(ref left, 2) = left;
                    Unsafe.Subtract(ref right, 2) = right;
                }
            }

            left = ref Unsafe.Dec(ref right);
            if (Unsafe.IsAddressLessThan(ref left, ref last))
            {
                Unsafe.Subtract(ref left, 2) = left;
            }
        }

        // array[buffer .. start - 1] <=> "scrolling buffer"
        // 
        // "scrolling buffer" + array[start, middle - 1] + array[middle, end - 1]
        // --> array[buffer, buffer + end - 1] + "scrolling buffer"
        [OverloadTemplate(nameof(T), nameof(comp), nameof(span))]
        static void MergeForwards(Span<T> span, int bufferOffset, int leftLen, Comparison<T> comp)
        {
            ref T buffer = ref span.Ref(0);
            ref T left = ref span.Ref(bufferOffset);
            ref T middle = ref Unsafe.Add(ref left, leftLen);
            ref T right = ref middle;
            ref T last = ref span.Ref(span.Length);

            while (Unsafe.IsAddressLessThan(in right, in last))
            {
                if (Unsafe.AreSame(in left, in middle) || Less(in right, in left, comp))
                {
                    Swap(ref buffer, ref right);
                    right = ref Unsafe.Inc(ref right);
                }
                else
                {
                    Swap(ref buffer, ref left);
                    left = ref Unsafe.Inc(ref left);
                }
                buffer = ref Unsafe.Inc(ref buffer);
            }

            if (!Unsafe.AreSame(in buffer, in left))
            {
                SwapBlock(ref buffer, ref left, Unsafe.Offset(in left, in middle));
            }
        }

        // credit to 666666t for thorough bug-checking/fixing
        [OverloadTemplate(nameof(T), nameof(comp), nameof(span))]
        static void MergeBackwards(Span<T> span, int leftLen, int bufferOffset, Comparison<T> comp)
        {
            ref T last = ref span.Ref(-1);
            ref T left = ref Unsafe.Add(ref last, leftLen);
            ref T middle = ref left;
            ref T buffer = ref span.Ref(span.Length - 1);
            ref T right = ref Unsafe.Subtract(ref buffer, bufferOffset);

            while (Unsafe.IsAddressGreaterThan(in left, in last))
            {
                if (Unsafe.AreSame(in right, in middle) || Less(in right, in left, comp))
                {
                    Swap(ref buffer, ref left);
                    left = ref Unsafe.Dec(ref left);
                }
                else
                {
                    Swap(ref buffer, ref right);
                    right = ref Unsafe.Dec(ref right);
                }
                buffer = ref Unsafe.Dec(ref buffer);
            }

            if (!Unsafe.AreSame(in buffer, in right))
            {
                SwapBlockBackward(ref buffer, ref right, Unsafe.Offset(in middle, in right));
            }
        }

        // "Classic" in-place merge sort using binary searches and rotations
        //
        // cost: min(leftLen, rightLen)^2 + max(leftLen, rightLen)
        // MINOR CHANGES: better naming -- 'insertPos' is now 'mergeLen' -- and "middle"/"end" calculations simplified
        [OverloadTemplate(nameof(T), nameof(comp), nameof(span))]
        static void MergeLazy(Span<T> span, int middle, ref readonly Context<T> context, Comparison<T> comp)
        {
            context.Destruct(out Span<T> extBuffer);

            if (middle < span.Length - middle)
            {
                int start = 0;

                while (start != middle)
                {
                    int mergeLen = LowerBound(span.Sub(middle, span.Length), in span.Ref(start), comp);

                    if (mergeLen != 0)
                    {
                        Rotate(span.Sub(start, middle + mergeLen), middle - start, extBuffer);

                        start += mergeLen;
                        middle += mergeLen;
                    }

                    if (middle == span.Length)
                    {
                        return;
                    }

                    do start++;
                    while (start != middle && !Less(in span.Ref(middle), in span.Ref(start), comp));
                }
            }
            // INDEXING BUG FIXED: Credit to Anonymous0726 for debugging.
            else
            {
                int end = span.Length - 1;

                while (middle <= end)
                {
                    int mergeLen = UpperBound(span.Sub(0, middle), in span.Ref(end), comp);

                    if (mergeLen != middle)
                    {
                        Rotate(span.Sub(mergeLen, end + 1), middle - mergeLen, extBuffer);

                        end -= middle - mergeLen;
                        middle = mergeLen;
                    }

                    if (middle == 0)
                    {
                        return;
                    }

                    do end--;
                    while (middle <= end && !Less(in span.Ref(end), in span.Ref(middle - 1), comp));
                }
            }
        }

        // array[buffer .. start - 1] <=> "free space"    
        //
        // "free space" + array[start, middle - 1] + array[middle, end - 1]
        // --> array[buffer, buffer + end - 1] + "free space"
        //
        // FUNCTION RENAMED: More consistent with "out-of-place" being at the end
        [OverloadTemplate(nameof(T), nameof(comp), nameof(span))]
        static void MergeOutOfPlace(Span<T> span, int bufferOffset, int leftLen, Comparison<T> comp)
        {
            ref T buffer = ref span.Ref(0);
            ref T left = ref span.Ref(bufferOffset);
            ref T middle = ref Unsafe.Add(ref left, leftLen);
            ref T right = ref middle;
            ref T last = ref span.Ref(span.Length);

            while (Unsafe.IsAddressLessThan(in right, in last))
            {
                if (Unsafe.AreSame(in left, in middle) || Less(in right, in left, comp))
                {
                    buffer = right;
                    right = ref Unsafe.Inc(ref right);
                }
                else
                {
                    buffer = left;
                    left = ref Unsafe.Inc(ref left);
                }
                buffer = ref Unsafe.Inc(ref buffer);
            }

            if (!Unsafe.AreSame(in buffer, in left))
            {
                span.Sub(span.Offset(in left), span.Offset(in middle)).CopyTo(span.Sub(span.Offset(in buffer), span.Length));
            }
        }

        [OverloadTemplate(nameof(T), nameof(comp), nameof(span))]
        static void BuildInPlace(Span<T> span, int start, int currentLen, int bufferLen, ref readonly Context<T> context, Comparison<T> comp)
        {
            int length = span.Length - start - currentLen;
            int fullMerge;
            context.Destruct(out Span<T> extBuffer);

            for (int mergeLen = currentLen; mergeLen < bufferLen; mergeLen *= 2)
            {
                Debug.Assert(start >= 0);

                fullMerge = 2 * mergeLen;

                int mergeIndex;
                int mergeEnd = start + length - fullMerge;
                int bufferOffset = mergeLen;

                for (mergeIndex = start; mergeIndex <= mergeEnd; mergeIndex += fullMerge)
                    MergeForwards(span.Sub(mergeIndex - bufferOffset, mergeIndex + fullMerge), bufferOffset, mergeLen, comp);

                int leftOver = length - (mergeIndex - start);

                if (leftOver > mergeLen)
                    MergeForwards(span.Sub(mergeIndex - bufferOffset, start + length), bufferOffset, mergeLen, comp);
                else
                    Rotate(span.Sub(mergeIndex - mergeLen, mergeIndex + leftOver), mergeLen, extBuffer);

                start -= mergeLen;
            }

            fullMerge = 2 * bufferLen;
            int lastBlock = length % fullMerge;
            int lastOffset = start + length - lastBlock;

            if (lastBlock <= bufferLen)
                Rotate(span.Sub(lastOffset, lastOffset + lastBlock + bufferLen), lastBlock, extBuffer);
            else
                MergeBackwards(span.Sub(lastOffset, lastOffset + bufferLen + lastBlock), bufferLen, bufferLen, comp);

            for (int mergeIndex = lastOffset - fullMerge; mergeIndex >= start; mergeIndex -= fullMerge)
                MergeBackwards(span.Sub(mergeIndex, mergeIndex + bufferLen * 3), bufferLen, bufferLen, comp);
        }

        [OverloadTemplate(nameof(T), nameof(comp), nameof(span))]
        static void BuildOutOfPlace(Span<T> span, int start, int bufferLen, ref readonly Context<T> context, Comparison<T> comp)
        {
            int extLen = bufferLen < context.ExtBuffer.Length
                ? bufferLen
                // max power of 2 -- just in case
                : 1 << BitOperations.Log2((uint)context.ExtBuffer.Length);
            context.Destruct(out Span<T> extBuffer);
            extBuffer = extBuffer.Sub(0, extLen);
            span.Sub(start - extBuffer.Length, start).CopyTo(extBuffer);

            Debug.Assert(start >= 0);
            int length = span.Length - start;
            PairwiseWrites(span.Sub(start - 2, span.Length), comp);
            start -= 2;

            int mergeLen;
            for (mergeLen = 2; mergeLen < extBuffer.Length; mergeLen *= 2)
            {
                int fullMerge = 2 * mergeLen;

                int mergeIndex;
                int mergeEnd = start + length - fullMerge;
                int bufferOffset = mergeLen;

                for (mergeIndex = start; mergeIndex <= mergeEnd; mergeIndex += fullMerge)
                    MergeOutOfPlace(span.Sub(mergeIndex - bufferOffset, mergeIndex + fullMerge), bufferOffset, mergeLen, comp);

                int leftOver = length - (mergeIndex - start);

                if (leftOver > mergeLen)
                    MergeOutOfPlace(span.Sub(mergeIndex - bufferOffset, start + length), bufferOffset, mergeLen, comp);
                else
                    span.Sub(mergeIndex, mergeIndex + leftOver).CopyTo(span.Sub(mergeIndex - mergeLen, span.Length));

                start -= mergeLen;
            }

            extBuffer.CopyTo(span.Sub(start + length, span.Length));
            BuildInPlace(span, start, mergeLen, bufferLen, in context, comp);
        }

        // build blocks of length 'bufferLen'
        // input: [start - mergeLen, start - 1] elements are buffer
        // output: first 'bufferLen' elements are buffer, blocks (2 * bufferLen) and last subblock sorted
        [OverloadTemplate(nameof(T), nameof(comp), nameof(span))]
        static void BuildBlocks(Span<T> span, int start, int bufferLen, ref readonly Context<T> context, Comparison<T> comp)
        {
            if (context.IsExtBufferIdeal && !context.ExtBuffer.IsEmpty)
            {
                BuildOutOfPlace(span, start, bufferLen, in context, comp);
            }
            else
            {
                Debug.Assert(start >= 2);
                PairwiseSwaps(span.Sub(start - 2, span.Length), comp);
                BuildInPlace(span, start - 2, 2, bufferLen, in context, comp);
            }
        }

        // Returns the final position of 'medianKey'.
        // MINOR CHANGES: Change comparison order to emphasize "less-than" relation; fewer variables (Credit to Anonymous0726 for better variable names!)
        [OverloadTemplate(nameof(T), nameof(comp), nameof(span))]
        static int BlockSelectSort(Span<T> span, int start, int medianKey, int blockCount, int blockLen, Comparison<T> comp)
        {
            for (int firstBlock = 0; firstBlock < blockCount; firstBlock++)
            {
                int selectBlock = firstBlock;

                for (int currBlock = firstBlock + 1; currBlock < blockCount; currBlock++)
                {
                    int cmp = Compare(in span.Ref(start + (currBlock * blockLen)), in span.Ref(start + (selectBlock * blockLen)), comp);
                    if (cmp < 0 ||
                        (cmp == 0 && Less(in span.Ref(currBlock), in span.Ref(selectBlock), comp)))
                        selectBlock = currBlock;
                }

                if (selectBlock != firstBlock)
                {
                    // Swap the left and right selected blocks...
                    SwapBlock(ref span.Ref(start + (firstBlock * blockLen)), ref span.Ref(start + (selectBlock * blockLen)), blockLen);

                    // Swap the keys...
                    Swap(ref span.Ref(firstBlock), ref span.Ref(selectBlock));

                    // ...and follow the 'medianKey' if it was swapped

                    // ORIGINAL LOC: if(midkey==u-1 || midkey==p) midkey^=(u-1)^p;
                    // MASSIVE, MASSIVE credit to lovebuny for figuring this one out!
                    if (medianKey == firstBlock)
                        medianKey = selectBlock;
                    else if (medianKey == selectBlock)
                        medianKey = firstBlock;
                }
            }

            return medianKey;
        }
    }

    // Swaps Grailsort's "scrolling buffer" from the right side of the array all the way back to 'start'.
    // Costs O(n) swaps.
    //
    // OFF-BY-ONE BUG FIXED: used to be `int index = start + resetLen`; credit to 666666t for debugging
    // RESTRUCTED, BETTER NAMES: 'resetLen' is now 'length' and 'bufferLen' is now 'bufferOffset'
    // SWAPPED NAMES: 'buffer' is now 'index' and vice versa
    [OverloadTemplate(nameof(T), null)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void InPlaceBufferReset<T>(Span<T> span, int start, int bufferOffset)
    {
        int buffer = span.Length - 1;
        SwapBlockBackward(ref span.Ref(buffer - bufferOffset), ref span.Ref(buffer), span.Length - start);
    }

    // Shifts entire array over 'bufferOffset' spaces to move the out-of-place merging buffer back to
    // the beginning of the array.
    // Costs O(n) writes.
    //
    // OFF-BY-ONE BUG FIXED: used to be `int index = start + resetLen`; credit to 666666t for debugging
    // RESTRUCTED, BETTER NAMES: 'resetLen' is now 'length' and 'bufferLen' is now 'bufferOffset'
    // SWAPPED NAMES: 'buffer' is now 'index' and vice versa
    [OverloadTemplate(nameof(T), null)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void OutOfPlaceBufferReset<T>(Span<T> span, int start, int bufferOffset)
    {
        span.Sub(start - bufferOffset, span.Length - bufferOffset).CopyTo(span.Sub(start, span.Length));
    }

    // Rewinds Grailsort's "scrolling buffer" to the left of any items belonging to the left subarray block
    // left over by a "smart merge". This is used to continue an ongoing merge that has run out of buffer space.
    // Costs O(sqrt n) swaps in the *absolute* worst-case. 
    //
    // BETTER ORDER-OF-OPERATIONS, NAMING IMPROVED: the left over items (now called 'leftBlock') are in the
    //                                              middle of the merge while the buffer is at the end
    [OverloadTemplate(nameof(T), null)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void InPlaceBufferRewind<T>(Span<T> span, int leftBlock)
    {
        SwapBlockBackward(ref span.Ref(leftBlock), ref span.Ref(span.Length - 1), leftBlock + 1);
    }

    // Rewinds Grailsort's out-of-place buffer to the left of any items belonging to the left subarray block
    // left over by a "smart merge". This is used to continue an ongoing merge that has run out of buffer space.
    // Costs O(sqrt n) writes in the *absolute* worst-case.
    //
    // BETTER ORDER, INCORRECT ORDER OF PARAMETERS BUG FIXED: `leftOvers` (now called 'leftBlock') should be
    //                                                        the middle, and `buffer` should be the end
    [OverloadTemplate(nameof(T), null)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static void OutOfPlaceBufferRewind<T>(Span<T> span, int leftBlock)
    {
        span.Sub(0, leftBlock + 1).CopyTo(span.Sub(span.Length - leftBlock - 1, span.Length));
    }

    partial class Fn<T>
    {
        [OverloadTemplate(nameof(T), nameof(comp))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Subarray GetSubarray(ref readonly T currentKey, ref readonly T medianKey, Comparison<T> comp)
        {
            return Less(in currentKey, in medianKey, comp) ? Subarray.Left : Subarray.Right;
        }

        // FUNCTION RE-RENAMED: last/final left blocks are used to calculate the length of the final merge
        [OverloadTemplate(nameof(T), nameof(comp))]
        static int CountLastMergeBlocks(ReadOnlySpan<T> span, int blockCount, int blockLen, Comparison<T> comp)
        {
            int blocksToMerge = 0;

            int lastRightFrag = blockCount * blockLen;
            int prevLeftBlock = lastRightFrag - blockLen;

            while (blocksToMerge < blockCount && Less(in span.Ref(lastRightFrag), in span.Ref(prevLeftBlock), comp))
            {
                blocksToMerge++;
                prevLeftBlock -= blockLen;
            }

            return blocksToMerge;
        }

        [OverloadTemplate(nameof(T), nameof(comp), nameof(span))]
        static void SmartMerge(Span<T> span, int bufferOffset, int leftLen, Subarray leftOrigin, ref Context<T> context, Comparison<T> comp)
        {
            ref T buffer = ref span.Ref(0);
            ref T left = ref span.Ref(bufferOffset);
            ref T middle = ref Unsafe.Add(ref left, leftLen);
            ref T right = ref middle;
            ref T last = ref span.Ref(span.Length);

            if (leftOrigin == Subarray.Left)
            {
                while (Unsafe.IsAddressLessThan(in left, in middle) && Unsafe.IsAddressLessThan(in right, in last))
                {
                    if (!Less(in right, in left, comp))
                    {
                        Swap(ref buffer, ref left);
                        left = ref Unsafe.Inc(ref left);
                    }
                    else
                    {
                        Swap(ref buffer, ref right);
                        right = ref Unsafe.Inc(ref right);
                    }
                    buffer = ref Unsafe.Inc(ref buffer);
                }
            }
            else
            {
                while (Unsafe.IsAddressLessThan(in left, in middle) && Unsafe.IsAddressLessThan(in right, in last))
                {
                    if (Less(in left, in right, comp))
                    {
                        Swap(ref buffer, ref left);
                        left = ref Unsafe.Inc(ref left);
                    }
                    else
                    {
                        Swap(ref buffer, ref right);
                        right = ref Unsafe.Inc(ref right);
                    }
                    buffer = ref Unsafe.Inc(ref buffer);
                }
            }

            if (Unsafe.IsAddressLessThan(in left, in middle))
            {
                context.CurrBlockLen = Unsafe.Offset(in left, in middle);
                // UPDATED ARGUMENTS: 'middle' and 'end' now 'middle - 1' and 'end - 1'
                InPlaceBufferRewind(span.Sub(span.Offset(in left), span.Length), Unsafe.Offset(in left, in middle) - 1);
            }
            else
            {
                context.CurrBlockLen = Unsafe.Offset(in right, in last);
                context.CurrBlockOrigin = Flip(leftOrigin);
            }
        }

        // MINOR CHANGE: better naming -- 'insertPos' is now 'mergeLen' -- and "middle" calculation simplified
        [OverloadTemplate(nameof(T), nameof(comp), nameof(span))]
        static void SmartMergeLazy(Span<T> span, int middle, Subarray leftOrigin, ref Context<T> context, Comparison<T> comp)
        {
            int start = 0;
            context.Destruct(out Span<T> extBuffer);

            if (leftOrigin == Subarray.Left)
            {
                if (Less(in span.Ref(middle), in span.Ref(middle - 1), comp))
                {
                    while (start != middle)
                    {
                        int mergeLen = LowerBound(span.Sub(middle, span.Length), in span.Ref(start), comp);

                        if (mergeLen != 0)
                        {
                            Rotate(span.Sub(start, middle + mergeLen), middle - start, extBuffer);

                            start += mergeLen;
                            middle += mergeLen;
                        }

                        if (middle == span.Length)
                        {
                            context.CurrBlockLen = middle - start;
                            return;
                        }

                        do start++;
                        while (start != middle && !Less(in span.Ref(middle), in span.Ref(start), comp));
                    }
                }
            }
            else
            {
                if (!Less(in span.Ref(middle - 1), in span.Ref(middle), comp))
                {
                    while (start != middle)
                    {
                        int mergeLen = UpperBound(span.Sub(middle, span.Length), in span.Ref(start), comp);

                        if (mergeLen != 0)
                        {
                            Rotate(span.Sub(start, middle + mergeLen), middle - start, extBuffer);

                            start += mergeLen;
                            middle += mergeLen;
                        }

                        if (middle == span.Length)
                        {
                            context.CurrBlockLen = middle - start;
                            return;
                        }

                        do start++;
                        while (start != middle && Less(in span.Ref(start), in span.Ref(middle), comp));
                    }
                }
            }

            context.CurrBlockLen = span.Length - middle;
            context.CurrBlockOrigin = Flip(leftOrigin);
        }

        // FUNCTION RENAMED: more consistent with other "out-of-place" merges
        [OverloadTemplate(nameof(T), nameof(comp), nameof(span))]
        static void SmartMergeOutOfPlace(Span<T> span, int bufferOffset, int leftLen, Subarray leftOrigin, ref Context<T> context, Comparison<T> comp)
        {
            ref T buffer = ref span.Ref(0);
            ref T left = ref span.Ref(bufferOffset);
            ref T middle = ref Unsafe.Add(ref left, leftLen);
            ref T right = ref middle;
            ref T last = ref span.Ref(span.Length);

            if (leftOrigin == Subarray.Left)
            {
                while (Unsafe.IsAddressLessThan(in left, in middle) && Unsafe.IsAddressLessThan(in right, in last))
                {
                    if (!Less(in right, in left, comp))
                    {
                        buffer = left;
                        left = ref Unsafe.Inc(ref left);
                    }
                    else
                    {
                        buffer = right;
                        right = ref Unsafe.Inc(ref right);
                    }
                    buffer = ref Unsafe.Inc(ref buffer);
                }
            }
            else
            {
                while (Unsafe.IsAddressLessThan(in left, in middle) && Unsafe.IsAddressLessThan(in right, in last))
                {
                    if (Less(in left, in right, comp))
                    {
                        buffer = left;
                        left = ref Unsafe.Inc(ref left);
                    }
                    else
                    {
                        buffer = right;
                        right = ref Unsafe.Inc(ref right);
                    }
                    buffer = ref Unsafe.Inc(ref buffer);
                }
            }


            if (Unsafe.IsAddressLessThan(in left, in middle))
            {
                context.CurrBlockLen = Unsafe.Offset(in left, in middle);
                // UPDATED ARGUMENTS: 'middle' and 'end' now 'middle - 1' and 'end - 1'
                OutOfPlaceBufferRewind(span.Sub(span.Offset(in left), span.Length), Unsafe.Offset(in left, in middle) - 1);
            }
            else
            {
                context.CurrBlockLen = Unsafe.Offset(in right, in last);
                context.CurrBlockOrigin = Flip(leftOrigin);
            }
        }

        // Credit to Anonymous0726 for better variable names such as "nextBlock"
        // Also minor change: removed unnecessary "currBlock = nextBlock" lines
        [OverloadTemplate(nameof(T), nameof(comp), nameof(span))]
        static void MergeBlocks(Span<T> span, int start, int medianKey, int blockCount, int blockLen, int lastMergeBlocks, int lastLen, ref Context<T> context, Comparison<T> comp)
        {
            int buffer;
            int currBlock;
            int nextBlock = start + blockLen;

            context.CurrBlockLen = blockLen;
            context.CurrBlockOrigin = GetSubarray(in span.Ref(0), in span.Ref(medianKey), comp);

            for (int keyIndex = 1; keyIndex < blockCount; keyIndex++, nextBlock += blockLen)
            {
                currBlock = nextBlock - context.CurrBlockLen;
                Subarray nextBlockOrigin = GetSubarray(in span.Ref(keyIndex), in span.Ref(medianKey), comp);

                if (nextBlockOrigin == context.CurrBlockOrigin)
                {
                    buffer = currBlock - blockLen;

                    SwapBlock(ref span.Ref(buffer), ref span.Ref(currBlock), context.CurrBlockLen);
                    context.CurrBlockLen = blockLen;
                }
                else
                {
                    SmartMerge(span.Sub(currBlock - blockLen, currBlock + context.CurrBlockLen + blockLen), blockLen, context.CurrBlockLen, context.CurrBlockOrigin, ref context, comp);
                }
            }

            currBlock = nextBlock - context.CurrBlockLen;
            buffer = currBlock - blockLen;

            if (lastLen != 0)
            {
                if (context.CurrBlockOrigin == Subarray.Right)
                {
                    SwapBlock(ref span.Ref(buffer), ref span.Ref(currBlock), context.CurrBlockLen);

                    currBlock = nextBlock;
                    context.CurrBlockLen = blockLen * lastMergeBlocks;
                    context.CurrBlockOrigin = Subarray.Left;
                }
                else
                {
                    context.CurrBlockLen += blockLen * lastMergeBlocks;
                }

                MergeForwards(span.Sub(currBlock - blockLen, currBlock + context.CurrBlockLen + lastLen), blockLen, context.CurrBlockLen, comp);
            }
            else
            {
                SwapBlock(ref span.Ref(buffer), ref span.Ref(currBlock), context.CurrBlockLen);
            }
        }

        [OverloadTemplate(nameof(T), nameof(comp), nameof(span))]
        static void MergeBlocksLazy(Span<T> span, int start, int medianKey, int blockCount, int blockLen, int lastMergeBlocks, int lastLen, ref Context<T> context, Comparison<T> comp)
        {
            int currBlock;
            int nextBlock = start + blockLen;

            context.CurrBlockLen = blockLen;
            context.CurrBlockOrigin = GetSubarray(in span.Ref(0), in span.Ref(medianKey), comp);

            for (int keyIndex = 1; keyIndex < blockCount; keyIndex++, nextBlock += blockLen)
            {
                currBlock = nextBlock - context.CurrBlockLen;
                Subarray nextBlockOrigin = GetSubarray(in span.Ref(keyIndex), in span.Ref(medianKey), comp);

                if (nextBlockOrigin == context.CurrBlockOrigin)
                {
                    context.CurrBlockLen = blockLen;
                }
                else
                {
                    // These checks were included in the original code... but why???
                    if (blockLen != 0 && context.CurrBlockLen != 0)
                        SmartMergeLazy(span.Sub(currBlock, currBlock + context.CurrBlockLen + blockLen), context.CurrBlockLen, context.CurrBlockOrigin, ref context, comp);
                }
            }

            currBlock = nextBlock - context.CurrBlockLen;

            if (lastLen != 0)
            {
                if (context.CurrBlockOrigin == Subarray.Right)
                {
                    currBlock = nextBlock;
                    context.CurrBlockLen = blockLen * lastMergeBlocks;
                    context.CurrBlockOrigin = Subarray.Left;
                }
                else
                {
                    context.CurrBlockLen += blockLen * lastMergeBlocks;
                }

                MergeLazy(span.Sub(currBlock, currBlock + context.CurrBlockLen + lastLen), context.CurrBlockLen, in context, comp);
            }
        }

        [OverloadTemplate(nameof(T), nameof(comp), nameof(span))]
        static void MergeBlocksOutOfPlace(Span<T> span, int start, int medianKey, int blockCount, int blockLen, int lastMergeBlocks, int lastLen, ref Context<T> context, Comparison<T> comp)
        {
            int buffer;
            int currBlock;
            int nextBlock = start + blockLen;

            context.CurrBlockLen = blockLen;
            context.CurrBlockOrigin = GetSubarray(in span.Ref(0), in span.Ref(medianKey), comp);

            for (int keyIndex = 1; keyIndex < blockCount; keyIndex++, nextBlock += blockLen)
            {
                currBlock = nextBlock - context.CurrBlockLen;
                Subarray nextBlockOrigin = GetSubarray(in span.Ref(keyIndex), in span.Ref(medianKey), comp);

                if (nextBlockOrigin == context.CurrBlockOrigin)
                {
                    buffer = currBlock - blockLen;

                    span.Sub(currBlock, currBlock + context.CurrBlockLen).CopyTo(span.Sub(buffer, span.Length));
                    context.CurrBlockLen = blockLen;
                }
                else
                {
                    SmartMergeOutOfPlace(span.Sub(currBlock - blockLen, currBlock + context.CurrBlockLen + blockLen), blockLen, context.CurrBlockLen, context.CurrBlockOrigin, ref context, comp);
                }
            }

            currBlock = nextBlock - context.CurrBlockLen;
            buffer = currBlock - blockLen;

            if (lastLen != 0)
            {
                if (context.CurrBlockOrigin == Subarray.Right)
                {
                    span.Sub(currBlock, currBlock + context.CurrBlockLen).CopyTo(span.Sub(buffer, span.Length));

                    currBlock = nextBlock;
                    context.CurrBlockLen = blockLen * lastMergeBlocks;
                    context.CurrBlockOrigin = Subarray.Left;
                }
                else
                {
                    context.CurrBlockLen += blockLen * lastMergeBlocks;
                }

                MergeOutOfPlace(span.Sub(currBlock - blockLen, currBlock + context.CurrBlockLen + lastLen), blockLen, context.CurrBlockLen, comp);
            }
            else
            {
                span.Sub(currBlock, currBlock + context.CurrBlockLen).CopyTo(span.Sub(buffer, span.Length));
            }
        }

        //TODO: Double-check "Merge Blocks" arguments
        [OverloadTemplate(nameof(T), nameof(comp), nameof(span))]
        static void CombineInPlace(Span<T> span, int start, int subarrayLen, int blockLen, int mergeCount, int lastSubarrays, bool buffer, ref Context<T> context, Comparison<T> comp)
        {
            int fullMerge = 2 * subarrayLen;
            // SLIGHT OPTIMIZATION: 'blockCount' only needs to be calculated once for regular merges
            int blockCount = fullMerge / blockLen;

            for (int mergeIndex = 0; mergeIndex < mergeCount; mergeIndex++)
            {
                int offset = start + (mergeIndex * fullMerge);

                InsertionSort(ref span.Ref(0), blockCount, comp);

                // INCORRECT PARAMETER BUG FIXED: `block select sort` should be using `offset`, not `start`
                int medianKey = subarrayLen / blockLen;
                medianKey = BlockSelectSort(span, offset, medianKey, blockCount, blockLen, comp);

                if (buffer)
                    MergeBlocks(span, offset, medianKey, blockCount, blockLen, 0, 0, ref context, comp);
                else
                    MergeBlocksLazy(span, offset, medianKey, blockCount, blockLen, 0, 0, ref context, comp);
            }

            // INCORRECT CONDITIONAL/PARAMETER BUG FIXED: Credit to 666666t for debugging.
            if (lastSubarrays != 0)
            {
                int offset = start + (mergeCount * fullMerge);
                blockCount = lastSubarrays / blockLen;

                InsertionSort(ref span.Ref(0), blockCount + 1, comp);

                // INCORRECT PARAMETER BUG FIXED: `block select sort` should be using `offset`, not `start`
                int medianKey = subarrayLen / blockLen;
                medianKey = BlockSelectSort(span, offset, medianKey, blockCount, blockLen, comp);

                // MISSING BOUNDS CHECK BUG FIXED: `lastFragment` *can* be 0 if the last two subarrays are evenly
                //                                 divided into blocks. This prevents Grailsort from going out-of-bounds.
                int lastFragment = lastSubarrays - (blockCount * blockLen);
                int lastMergeBlocks = lastFragment != 0
                    ? CountLastMergeBlocks(span.Sub(offset, span.Length), blockCount, blockLen, comp)
                    : 0;

                int smartMerges = blockCount - lastMergeBlocks;

                //TODO: Double-check if this micro-optimization works correctly like the original
                if (smartMerges == 0)
                {
                    // MINOR CHANGE: renamed for consistency (used to be 'leftLength')
                    int leftLen = lastMergeBlocks * blockLen;

                    // INCORRECT PARAMETER BUG FIXED: these merges should be using `offset`, not `start`
                    if (buffer)
                        MergeForwards(span.Sub(offset - blockLen, offset + leftLen + lastFragment), blockLen, leftLen, comp);
                    else
                        MergeLazy(span.Sub(offset, offset + leftLen + lastFragment), leftLen, in context, comp);
                }
                else
                {
                    if (buffer)
                        MergeBlocks(span, offset, medianKey, smartMerges, blockLen, lastMergeBlocks, lastFragment, ref context, comp);
                    else
                        MergeBlocksLazy(span, offset, medianKey, smartMerges, blockLen, lastMergeBlocks, lastFragment, ref context, comp);
                }
            }

            if (buffer)
                InPlaceBufferReset(span, start, blockLen);
        }

        [OverloadTemplate(nameof(T), nameof(comp), nameof(span))]
        static void CombineOutOfPlace(Span<T> span, int start, int subarrayLen, int blockLen, int mergeCount, int lastSubarrays, ref Context<T> context, Comparison<T> comp)
        {
            context.Destruct(out Span<T> extBuffer);
            span.Sub(start - blockLen, start).CopyTo(extBuffer);

            int fullMerge = 2 * subarrayLen;
            // SLIGHT OPTIMIZATION: 'blockCount' only needs to be calculated once for regular merges
            int blockCount = fullMerge / blockLen;

            for (int mergeIndex = 0; mergeIndex < mergeCount; mergeIndex++)
            {
                int offset = start + (mergeIndex * fullMerge);

                InsertionSort(ref span.Ref(0), blockCount, comp);

                // INCORRECT PARAMETER BUG FIXED: `block select sort` should be using `offset`, not `start`
                int medianKey = subarrayLen / blockLen;
                medianKey = BlockSelectSort(span, offset, medianKey, blockCount, blockLen, comp);

                MergeBlocksOutOfPlace(span, offset, medianKey, blockCount, blockLen, 0, 0, ref context, comp);
            }

            // INCORRECT CONDITIONAL/PARAMETER BUG FIXED: Credit to 666666t for debugging.
            if (lastSubarrays != 0)
            {
                int offset = start + (mergeCount * fullMerge);
                blockCount = lastSubarrays / blockLen;

                InsertionSort(ref span.Ref(0), blockCount + 1, comp);

                // INCORRECT PARAMETER BUG FIXED: `block select sort` should be using `offset`, not `start`
                int medianKey = subarrayLen / blockLen;
                medianKey = BlockSelectSort(span, offset, medianKey, blockCount, blockLen, comp);

                // MISSING BOUNDS CHECK BUG FIXED: `lastFragment` *can* be 0 if the last two subarrays are evenly
                //                                 divided into blocks. This prevents Grailsort from going out-of-bounds.
                int lastFragment = lastSubarrays - (blockCount * blockLen);
                int lastMergeBlocks = lastFragment != 0
                    ? CountLastMergeBlocks(span.Sub(offset, span.Length), blockCount, blockLen, comp)
                    : 0;

                int smartMerges = blockCount - lastMergeBlocks;

                //TODO: Double-check if this micro-optimization works correctly like the original
                if (smartMerges == 0)
                {
                    // MINOR CHANGE: renamed for consistency (used to be 'leftLength')
                    int leftLen = lastMergeBlocks * blockLen;

                    // INCORRECT PARAMETER BUG FIXED: these merges should be using `offset`, not `start`
                    MergeOutOfPlace(span.Sub(offset - blockLen, offset + leftLen + lastFragment), blockLen, leftLen, comp);
                }
                else
                {
                    MergeBlocksOutOfPlace(span, offset, medianKey, smartMerges, blockLen, lastMergeBlocks, lastFragment, ref context, comp);
                }
            }

            OutOfPlaceBufferReset(span, start, blockLen);
            extBuffer.Sub(0, blockLen).CopyTo(span.Sub(start - blockLen, span.Length));
        }

        // 'keys' are on the left side of array. Blocks of length 'subarrayLen' combined. We'll combine them in pairs
        // 'subarrayLen' is a power of 2. (2 * subarrayLen / blockLen) keys are guaranteed
        //
        // IMPORTANT RENAME: 'lastSubarray' is now 'lastSubarrays' because it includes the length of the last left
        //                   subarray AND last right subarray (if there is a right subarray at all).
        //
        //                   *Please also check everything surrounding 'if(lastSubarrays != 0)' inside
        //                   'combine in-/out-of-place' methods for other renames!!*
        [OverloadTemplate(nameof(T), nameof(comp), nameof(span))]
        static void CombineBlocks(Span<T> span, int start, int subarrayLen, int blockLen, bool buffer, ref Context<T> context, Comparison<T> comp)
        {
            int length = span.Length - start;
            int fullMerge = 2 * subarrayLen;
            int mergeCount = length / fullMerge;
            int lastSubarrays = length - (fullMerge * mergeCount);

            if (lastSubarrays <= subarrayLen)
            {
                length -= lastSubarrays;
                lastSubarrays = 0;
            }

            // INCOMPLETE CONDITIONAL BUG FIXED: In order to combine blocks out-of-place, we must check if a full-sized
            //                                   block fits into our external buffer.
            if (buffer && blockLen <= context.ExtBuffer.Length)
                CombineOutOfPlace(span.Sub(0, start + length), start, subarrayLen, blockLen, mergeCount, lastSubarrays, ref context, comp);
            else
                CombineInPlace(span.Sub(0, start + length), start, subarrayLen, blockLen, mergeCount, lastSubarrays, buffer, ref context, comp);
        }

        [OverloadTemplate(nameof(T), nameof(comp), nameof(span))]
        static void StableSortLazy(Span<T> span, ref readonly Context<T> context, Comparison<T> comp)
        {
            ref T right = ref span.Ref(1);
            ref T last = ref span.Ref(span.Length);
            for (; Unsafe.IsAddressLessThan(in right, in last); right = ref Unsafe.Add(ref right, 2))
            {
                CmpEx(ref Unsafe.Dec(ref right), ref right, comp);
            }
            for (int mergeLen = 2; mergeLen < span.Length; mergeLen *= 2)
            {
                int fullMerge = 2 * mergeLen;

                int mergeIndex;
                int mergeEnd = span.Length - fullMerge;

                for (mergeIndex = 0; mergeIndex <= mergeEnd; mergeIndex += fullMerge)
                    MergeLazy(span.Sub(mergeIndex, mergeIndex + fullMerge), mergeLen, in context, comp);

                int leftOver = span.Length - mergeIndex;
                if (leftOver > mergeLen)
                    MergeLazy(span.Sub(mergeIndex, span.Length), mergeLen, in context, comp);
            }
        }

        [OverloadTemplate(nameof(T), nameof(comp), nameof(span), nameof(extBuffer))]
        static void SortLoop(Span<T> span, Span<T> extBuffer, Comparison<T> comp)
        {
            // find the smallest power of two greater than or equal to
            // the square root of the input's length
            int blockLen = 1 << ((BitOperations.Log2((uint)span.Length - 1) + 2) / 2);

            // '((a - 1) / b) + 1' is actually a clever and very efficient
            // formula for the ceiling of (a / b)
            //
            // credit to Anonymous0726 for figuring this out!
            int keyLen = ((span.Length - 1) / blockLen) + 1;

            // Grailsort is hoping to find `2 * sqrt(n)` unique items
            // throughout the array
            int idealKeys = keyLen + blockLen;

            //TODO: Clean up `start +` offsets
            Context<T> context = Context<T>.FromBuffer(extBuffer);
            int keysFound = CollectKeys(span, idealKeys, in context, comp);

            if (keysFound < idealKeys)
            {
                if (keysFound < 4)
                {
                    // GRAILSORT STRATEGY 3 -- No block swaps or scrolling buffer; resort to Lazy Stable Sort
                    StableSortLazy(span, in context, comp);
                    return;
                }
                else
                {
                    // GRAILSORT STRATEGY 2 -- Block swaps with small scrolling buffer and/or lazy merges
                    keyLen = Math.Min(blockLen, 1 << BitOperations.Log2((uint)keysFound));
                    blockLen = 0;
                    context.IsExtBufferIdeal = false;
                }
            }
            else
            {
                // GRAILSORT STRATEGY 1 -- Block swaps with scrolling buffer
                context.IsExtBufferIdeal = true;
            }

            int bufferEnd = blockLen + keyLen;
            int subarrayLen = context.IsExtBufferIdeal ? blockLen : keyLen;

            // GRAILSORT + EXTRA SPACE
            // if (!idealBuffer)
            //     extBuffer = default;

            BuildBlocks(span, bufferEnd, subarrayLen, in context, comp);

            while ((span.Length - bufferEnd) > (2 * subarrayLen))
            {
                subarrayLen *= 2;

                int currentBlockLen = blockLen;
                bool scrollingBuffer = context.IsExtBufferIdeal;

                // Huge credit to Anonymous0726, phoenixbound, and DeveloperSort for their tireless efforts
                // towards deconstructing this math.
                if (!context.IsExtBufferIdeal)
                {
                    int keyBuffer = keyLen / 2;

                    // TODO: Rewrite explanation for this math
                    if (keyBuffer >= ((2 * subarrayLen) / keyBuffer))
                    {
                        currentBlockLen = keyBuffer;
                        scrollingBuffer = true;
                    }
                    else
                    {
                        // This is a very recent discovery, and the math will be spelled out later, but this
                        // "minKeys" calculation is *completely unnecessary*. "minKeys" would be less than
                        // "keyLen" iff ((keyBuffer >= (2 * subarrayLen)) / keyBuffer)... but this situation
                        // is already covered by our scrolling buffer optimization right above!! Consequently,
                        // "minKeys" will *always* be equal to "keyLen" when Grailsort resorts to smart lazy
                        // merges. Removing this loop is by itself a decent optimization, as well!
                        //
                        // Code still here for preservation purposes.
                        /*
                         * long subarrayKeys = ((long) subarrayLen * keysFound) / 2;
                         * int minKeys = grailCalcMinKeys(keyLen, subarrayKeys);
                         *
                         * currentBlockLen = (2 * subarrayLen) / minKeys;
                         */

                        currentBlockLen = 2 * subarrayLen / keyLen;
                    }
                }

                // WRONG VARIABLE BUG FIXED: 4th argument should be `length - bufferEnd`, was `length - bufferLen` before.
                // Credit to 666666t and Anonymous0726 for debugging.
                CombineBlocks(span, bufferEnd, subarrayLen, currentBlockLen, scrollingBuffer, ref context, comp);
            }

            InsertionSort(ref span.Ref(0), bufferEnd, comp);
            MergeLazy(span, bufferEnd, in context, comp);
        }

        [OverloadTemplate(nameof(T), nameof(comp), nameof(span))]
        [SkipLocalsInit]
        public static unsafe void Sort(Span<T> span, Comparison<T> comp, MemoryProfile profile)
        {
            if (span.Length < 16)
            {
                InsertionSort(ref span.Ref(0), span.Length, comp);
                return;
            }

            if (profile < MemoryProfile.Baseline)
                SortLoop(span, Span<T>.Empty, comp);
            else
            {
                var cacheSize = 1 << ((BitOperations.Log2((uint)span.Length - 1) + 2) / 2);

                if (profile < MemoryProfile.Medium || cacheSize <= StaticExtBufferLen)
                {
                    using MemoryOwner<T> owner = new();
                    Span<T> cache = RuntimeHelpers.IsReferenceOrContainsReferences<T>() || (nint)Unsafe.SizeOf<T>() * StaticExtBufferLen <= MaxStackAllocSize
                        ? owner.Attach(MemoryPool<T>.Shared.Rent(StaticExtBufferLen)).Memory.Span
                        : new Span<T>(
                            Unsafe.AsPointer(ref MemoryMarshal.GetReference(stackalloc byte[Unsafe.SizeOf<T>() * StaticExtBufferLen])),
                            StaticExtBufferLen);
                    SliceToLast(ref cache);
                    SortLoop(span, cache, comp);
                }
                else
                {
                    using MemoryOwner<T> owner = new(MemoryPool<T>.Shared.Rent(cacheSize));
                    Span<T> cache = owner.Memory.Span;
                    SliceToLast(ref cache);
                    SortLoop(span, cache, comp);
                }
            }
        }
    }
}
