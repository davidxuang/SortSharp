using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SortSharp.Compat;
using SortSharp.Foundation;
using SortSharp.SourceGenerators;

#if NET7_0_OR_GREATER
using System.Runtime.Intrinsics;
#endif

namespace SortSharp;

/// <summary>
/// Provides extension methods for <see cref="Span{T}"/>.
/// </summary>
public static partial class Extensions
{
    /// <summary>
    /// Rotates the elements of the specified <see cref="Span{T}"/> to the left by the specified number of positions.
    /// </summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="span">The elements to rotate.</param>
    /// <param name="left">The index of the beginning of the part to be moved to the left.</param>
    /// <param name="cache">Optional cache span to be used. It does not need to be cleared beforehand, and will not be cleared afterwards.</param>
    /// <seealso href="https://github.com/scandum/rotate"/>
    /// <exception cref="ArgumentException">
    /// The specified <paramref name="cache"/> overlaps with the input <paramref name="span"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The specified <paramref name="left"/> is out of the range of the <paramref name="span"/>.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Rotate<T>(this Span<T> span, int left, Span<T> cache = default)
    {
        ArgumentException.ThrowIf(span.Overlaps(cache), ArgumentException.MsgSpanOverlaps, nameof(cache));
        ArgumentOutOfRangeException.ThrowIfLessThan(left, 0, nameof(left));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(left, span.Length, nameof(left));

        SpanOperations.Rotate(span, left, cache);
    }
}

internal static partial class SpanOperations
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Ensure([DoesNotReturnIf(false)] bool condition)
    {
        if (!condition) ThrowHelper.ThrowInvalidOperation("The ordering invariant was violated. This may be caused by an inconsistent comparer or concurrent modification.");
        return true;
    }

    extension(Unsafe)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int Offset<T>(ref readonly T a, ref readonly T b)
            => (int)Unsafe.ByteOffset(in a, in b) / Unsafe.SizeOf<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref T Inc<T>(ref T source)
            => ref Unsafe.Add(ref source, 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref T Dec<T>(ref T source)
            => ref Unsafe.Subtract(ref source, 1);

        internal static UnsafeReadOnly RO
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => default;
        }
    }

    internal readonly struct UnsafeReadOnly
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref readonly TTo As<TFrom, TTo>(in TFrom source)
            => ref Unsafe.As<TFrom, TTo>(ref Unsafe.AsRef(in source));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref readonly T Inc<T>(ref readonly T source)
            => ref Unsafe.Add(ref Unsafe.AsRef(in source), 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref readonly T Dec<T>(ref readonly T source)
            => ref Unsafe.Subtract(ref Unsafe.AsRef(in source), 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref readonly T Add<T>(ref readonly T source, int elementOffset)
            => ref Unsafe.Add(ref Unsafe.AsRef(in source), elementOffset);
    }

    extension<T>(Span<T> span)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref T Ref(int index)
#if DEBUG
            => ref (index != -1 && index != span.Length // allows on-bound access
                ? ref span[index]
                : ref Unsafe.Add(ref MemoryMarshal.GetReference(span), index));
#else
            => ref Unsafe.Add(ref MemoryMarshal.GetReference(span), index);
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Span<T> Sub(Range range)
#if DEBUG || !NETSTANDARD2_1_COMPAT
            => span.Slice(range.Start, range.Length);
#else
            => MemoryMarshal.CreateSpan(ref span.Ref(range.Start), range.Length);
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Span<T> Sub(int start, int end)
#if DEBUG || !NETSTANDARD2_1_COMPAT
            => span.Slice(start, end - start);
#else
            => MemoryMarshal.CreateSpan(ref span.Ref(start), end - start);
#endif
    }

    extension<T>(ReadOnlySpan<T> span)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int Offset(ref readonly T value)
            => (int)Unsafe.ByteOffset(in MemoryMarshal.GetReference(span), in value) / Unsafe.SizeOf<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref readonly T Ref(int index)
#if DEBUG
            => ref (index != -1 && index != span.Length // allows on-bound access
                ? ref span[index]
                : ref Unsafe.Add(ref MemoryMarshal.GetReference(span), index));
#else
            => ref Unsafe.Add(ref MemoryMarshal.GetReference(span), index);
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlySpan<T> Sub(Range range)
#if DEBUG || !NETSTANDARD2_1_COMPAT
            => span.Slice(range.Start, range.Length);
#else
            => MemoryMarshal.CreateReadOnlySpan(in span.Ref(range.Start), range.Length);
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlySpan<T> Sub(int start, int end)
#if DEBUG || !NETSTANDARD2_1_COMPAT
            => span.Slice(start, end - start);
#else
            => MemoryMarshal.CreateReadOnlySpan(in span.Ref(start), end - start);
#endif
    }

    [ImplTemplate(nameof(T))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Swap<T>(ref T a, ref T b)
    {
        T t = a;
        a = b;
        b = t;
    }

    [ImplTemplate(nameof(T))]
    internal static void SwapBlock<T>(ref T a, ref T b, int length)
    {
        ref T last = ref Unsafe.Add(ref a, length);
        while (Unsafe.IsAddressLessThan(in a, in last))
        {
            Swap(ref a, ref b);
            a = ref Unsafe.Inc(ref a);
            b = ref Unsafe.Inc(ref b);
        }
    }

    [ImplTemplate(nameof(T))]
    internal static void SwapBlockBackward<T>(ref T a, ref T b, int length)
    {
        ref T last = ref Unsafe.Subtract(ref a, length);
        while (Unsafe.IsAddressGreaterThan(in a, in last))
        {
            Swap(ref a, ref b);
            a = ref Unsafe.Dec(ref a);
            b = ref Unsafe.Dec(ref b);
        }
    }

    private const nint MaxStackAllocSize = 1 << 16;

    [TypeArgumentExpansion]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool CanStackAlloc<T>(int length)
        => length == 0 || Unsafe.SizeOf<T>() <= MaxStackAllocSize / length;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool CanStackAlloc<T, U>(int length)
        => length == 0 || (RuntimeHelpers.IsReferenceOrContainsReferences<T>(), RuntimeHelpers.IsReferenceOrContainsReferences<U>()) switch
        {
            (false, false) => Unsafe.SizeOf<T>() + Unsafe.SizeOf<U>() <= MaxStackAllocSize / length,
            (false, true) => Unsafe.SizeOf<T>() <= MaxStackAllocSize / length,
            (true, false) => Unsafe.SizeOf<U>() <= MaxStackAllocSize / length,
            (true, true) => true, // will not stackalloc
        };

    [ImplTemplate(nameof(T), nameof(_), KeyValue = OverloadOption.Disable)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SliceToLast<T>(ref Span<T> _) { }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SliceToLast<T, V>(ref Span<T> a, ref readonly Span<V> b)
    {
        if (b.Length < a.Length) a = a.Sub(0, b.Length);
    }

    [ImplTemplate(nameof(T))]
    internal static void Rotate<T>(Span<T> span, int left, Span<T> cache = default)
    {
        Debug.Assert(0 <= left && left <= span.Length);

        int right = span.Length - left;
        if (left == 0 || right == 0)
            return; // 0

        ref T a = ref span.Ref(0);
        ref T d = ref span.Ref(span.Length);
        T swap;
        int loop;

        if (left > right)
        {
            if (right <= cache.Length)
            {
                span.Sub(left, span.Length).CopyTo(cache);
                span.Sub(0, left).CopyTo(span.Sub(right, span.Length));
                cache.Sub(0, right).CopyTo(span.Sub(0, right));
                return;
            }

            ref T b = ref span.Ref(left);
            ref T c = ref b;
            loop = left - right;

            if (loop <= cache.Length && loop > 3)
            {
                c = ref span.Ref(right);
                span.Sub(right, right + loop).CopyTo(cache);
                while (right-- > 0)
                {
                    c = a;
                    c = ref Unsafe.Inc(ref c);
                    a = b;
                    a = ref Unsafe.Inc(ref a);
                    b = ref Unsafe.Inc(ref b);
                }
                cache.Sub(0, loop).CopyTo(span.Sub(span.Length - loop, span.Length));
                return;
            }

            for (loop = right / 2; loop != 0; loop--)
            {
                b = ref Unsafe.Dec(ref b);
                swap = b;
                b = a;
                a = c;
                a = ref Unsafe.Inc(ref a);
                d = ref Unsafe.Dec(ref d);
                c = d;
                c = ref Unsafe.Inc(ref c);
                d = swap;
            }
            for (loop = Unsafe.Offset(ref a, ref b) / 2; loop != 0; loop--)
            {
                b = ref Unsafe.Dec(ref b);
                swap = b;
                b = a;
                d = ref Unsafe.Dec(ref d);
                a = d;
                a = ref Unsafe.Inc(ref a);
                d = swap;
            }
        }
        else if (left < right)
        {
            if (left <= cache.Length)
            {
                span.Sub(0, left).CopyTo(cache);
                span.Sub(left, span.Length).CopyTo(span.Sub(0, right));
                cache.Sub(0, left).CopyTo(span.Sub(right, span.Length));
                return;
            }

            ref T b = ref span.Ref(left);
            ref T c = ref b;
            loop = right - left;

            if (loop <= cache.Length && loop > 3)
            {
                c = ref span.Ref(right);
                span.Sub(left, left + loop).CopyTo(cache);
                while (left-- > 0)
                {
                    c = ref Unsafe.Dec(ref c);
                    d = ref Unsafe.Dec(ref d);
                    c = d;
                    b = ref Unsafe.Dec(ref b);
                    d = b;
                }
                cache.Sub(0, loop).CopyTo(span);
                return;
            }

            for (loop = left / 2; loop != 0; loop--)
            {
                b = ref Unsafe.Dec(ref b);
                swap = b;
                b = a;
                a = c;
                a = ref Unsafe.Inc(ref a);
                d = ref Unsafe.Dec(ref d);
                c = d;
                c = ref Unsafe.Inc(ref c);
                d = swap;
            }
            for (loop = Unsafe.Offset(ref c, ref d) / 2; loop != 0; loop--)
            {
                swap = c;
                d = ref Unsafe.Dec(ref d);
                c = d;
                c = ref Unsafe.Inc(ref c);
                d = a;
                a = swap;
                a = ref Unsafe.Inc(ref a);
            }
        }
        else
        {
            ref T b = ref span.Ref(left);

            for (loop = left; loop != 0; loop--)
            {
                swap = a;
                a = b;
                a = ref Unsafe.Inc(ref a);
                b = swap;
                b = ref Unsafe.Inc(ref b);
            }
            return; // !!!
        }

        for (loop = Unsafe.Offset(ref a, ref d) / 2; loop != 0; loop--)
        {
            swap = a;
            d = ref Unsafe.Dec(ref d);
            a = d;
            a = ref Unsafe.Inc(ref a);
            d = swap;
        }

        // return end - (mid - begin);
    }

    /// <seealso href="https://en.algorithmica.org/hpc/algorithms/prefix/"/>
    internal static void PartialSums(Span<int> counts)
    {
        int i = 0;
        int acc = 0;
#if NET8_0_OR_GREATER
        if (Vector512.IsHardwareAccelerated)
        {
            Debug.Assert(Vector512<int>.Count == 16);

            int end = counts.Length - counts.Length % 16;
            var A = Vector512<int>.Zero;
            var B = Vector512.Create(15);

            var S1 = Vector512.Create(0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14);
            var M1 = Vector512.Create(0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1);
            var S2 = Vector512.Create(0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13);
            var M2 = Vector512.Create(0, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1);
            var S4 = Vector512.Create(0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);
            var M4 = Vector512.Create(0, 0, 0, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1);
            var S8 = Vector512.Create(0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7);
            var M8 = Vector512.Create(0, 0, 0, 0, 0, 0, 0, 0, -1, -1, -1, -1, -1, -1, -1, -1);

            for (; i < end; i += 16)
            {
                var sub = counts.Sub(i, i + 16);
                var V = Vector512.Create(sub);
                V += (Vector512.Shuffle(V, S1) & M1);
                V += (Vector512.Shuffle(V, S2) & M2);
                V += (Vector512.Shuffle(V, S4) & M4);
                V += (Vector512.Shuffle(V, S8) & M8);
                A += V;
                A.CopyTo(sub);
                A = Vector512.Shuffle(A, B);
            }

            acc = A.GetElement(0);
        }
        else
#endif
#if NET7_0_OR_GREATER
        if (Vector256.IsHardwareAccelerated)
        {
            Debug.Assert(Vector256<int>.Count == 8);

            int end = counts.Length - counts.Length % 8;
            var A = Vector256<int>.Zero;
            var B = Vector256.Create(7);

            var S1 = Vector256.Create(0, 0, 1, 2, 3, 4, 5, 6);
            var M1 = Vector256.Create(0, -1, -1, -1, -1, -1, -1, -1);
            var S2 = Vector256.Create(0, 0, 0, 1, 2, 3, 4, 5);
            var M2 = Vector256.Create(0, 0, -1, -1, -1, -1, -1, -1);
            var S4 = Vector256.Create(0, 0, 0, 0, 0, 1, 2, 3);
            var M4 = Vector256.Create(0, 0, 0, 0, -1, -1, -1, -1);

            for (; i < end; i += 8)
            {
                var sub = counts.Sub(i, i + 8);
                var V = Vector256.Create(sub);
                V += (Vector256.Shuffle(V, S1) & M1);
                V += (Vector256.Shuffle(V, S2) & M2);
                V += (Vector256.Shuffle(V, S4) & M4);
                A += V;
                A.CopyTo(sub);
                A = Vector256.Shuffle(A, B);
            }

            acc = A.GetElement(0);
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            Debug.Assert(Vector128<int>.Count == 4);

            int end = counts.Length - counts.Length % 4;
            var A = Vector128<int>.Zero;
            var B = Vector128.Create(3);

            var S1 = Vector128.Create(0, 0, 1, 2);
            var M1 = Vector128.Create(0, -1, -1, -1);
            var S2 = Vector128.Create(0, 0, 0, 1);
            var M2 = Vector128.Create(0, 0, -1, -1);

            for (; i < end; i += 4)
            {
                var sub = counts.Sub(i, i + 4);
                var V = Vector128.Create(sub);
                V += (Vector128.Shuffle(V, S1) & M1);
                V += (Vector128.Shuffle(V, S2) & M2);
                A += V;
                A.CopyTo(sub);
                A = Vector128.Shuffle(A, B);
            }

            acc = A.GetElement(0);
        }
#endif

        for (; i < counts.Length; i++)
        {
            ref int count = ref counts.Ref(i);
            count += acc;
            acc = count;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static T? Extract<T>(ref readonly T? value) => value;

    [ImplTemplate(nameof(T), nameof(span), KeySelector = true)]
    internal static int MoveNullsToFront<T>(Span<T> span)
        where T : class
    {
        ref T first = ref span.Ref(0);
        ref T swap = ref span.Ref(0);
        ref T last = ref span.Ref(span.Length);
#pragma warning disable CS8601, CS8619
        while (!Unsafe.AreSame(in first, in last) && first is not null)
            first = ref Unsafe.Inc(ref first);
        for (; !Unsafe.AreSame(in first, in last); first = ref Unsafe.Inc(ref first))
        {
            if (Extract(in first) is null)
            {
                Swap(ref first, ref swap);
                swap = ref Unsafe.Inc(ref swap);
            }
        }
#pragma warning restore CS8601, CS8619
        return span.Offset(in swap);
    }

    [ImplTemplate(nameof(T), nameof(span), nameof(cache), KeyValue = OverloadOption.Disable)]
    internal static int MoveNullsToFrontStable<T>(Span<T> span, Span<T> cache)
        where T : class
    {
        Debug.Assert(span.Length == cache.Length);

        int j = 0;
        for (int i = 0; i < span.Length; i++)
        {
            ref T item = ref span.Ref(i);
            if (Extract(in item) is not null)
            {
                cache.Ref(j++) = item;
            }
        }
        int k = span.Length - j;
        span.Sub(0, k).Clear();
        cache.Sub(0, j).CopyTo(span.Sub(k, span.Length));
        return k;
    }

    [ImplTemplate(nameof(T), KeyValue = OverloadOption.Specialized)]
    internal static int MoveNullsToFrontStable<T, V>(Span<T> keys, Span<V> items, Span<T> cache, Span<V> cache_v)
        where T : class
    {
        Debug.Assert(keys.Length == items.Length);
        Debug.Assert(keys.Length == cache.Length);
        Debug.Assert(items.Length == cache_v.Length);

        int j = 0, k = cache.Length;
        for (int i = 0; i < keys.Length; i++)
        {
            ref T key = ref keys.Ref(i);
            ref V item = ref items.Ref(i);
            if (key is null)
            {
                cache_v.Ref(--k) = item;
            }
            else
            {
                cache.Ref(j) = key;
                cache_v.Ref(j) = item;
                j++;
            }
        }
        Ensure(j == k);
        k = keys.Length - j;
        keys.Sub(0, k).Clear();
        cache.Sub(0, j).CopyTo(keys.Sub(k, keys.Length));
        var sub = cache_v.Sub(j, cache_v.Length);
        sub.Reverse();
        sub.CopyTo(items);
        cache_v.Sub(0, j).CopyTo(items.Sub(k, items.Length));
        return k;
    }

#if NET7_0_OR_GREATER
    [ImplTemplate(nameof(T), nameof(span))]
    internal static int MoveNansToFront<T>(Span<T> span)
        where T : unmanaged, IFloatingPointIeee754<T>
    {
        ref T first = ref span.Ref(0);
        ref T swap = ref span.Ref(0);
        ref T last = ref span.Ref(span.Length);
        while (!Unsafe.AreSame(in first, in last) && !T.IsNaN(first))
            first = ref Unsafe.Inc(ref first);
        for (; !Unsafe.AreSame(in first, in last); first = ref Unsafe.Inc(ref first))
        {
            if (T.IsNaN(first))
            {
                Swap(ref first, ref swap);
                swap = ref Unsafe.Inc(ref swap);
            }
        }
        return span.Offset(in swap);
    }
#else
    [ImplTemplate(nameof(T), nameof(span))]
    internal static int MoveNansToFront<T>(Span<T> span)
        where T : unmanaged
    {
        Debug.Assert(typeof(T) == typeof(double) || typeof(T) == typeof(float)
#if NETSTANDARD2_0_COMPAT
            || typeof(T) == typeof(Half)
#endif
            );

        ref T first = ref span.Ref(0);
        ref T swap = ref span.Ref(0);
        ref T last = ref span.Ref(span.Length);
        for (; !Unsafe.AreSame(in first, in last); first = ref Unsafe.Inc(ref first))
        {
            if ((typeof(T) == typeof(double) && double.IsNaN((double)(object)first))
                || (typeof(T) == typeof(float) && float.IsNaN((float)(object)first))
#if NETSTANDARD2_0_COMPAT
                || (typeof(T) == typeof(Half) && Half.IsNaN((Half)(object)first))
#endif
                )
            {
                Swap(ref first, ref swap);
                swap = ref Unsafe.Inc(ref swap);
            }
        }
        return span.Offset(in swap);
    }
#endif

    internal static partial class From<V, T, TSelector>
#if NET7_0_OR_GREATER
        where TSelector : IKeySelector<V, T>
#else
        where TSelector : unmanaged, IKeySelector<V, T>
#endif
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static T? Extract(ref readonly V? value)
#if NET7_0_OR_GREATER
            => TSelector.Select(in value);
#else
            => default(TSelector).SelectInst(in value);
#endif

        internal static int MoveNullsToFrontStable(Span<V> span, Span<V> cache)
            //where T : class
        {
            Debug.Assert(!typeof(T).IsValueType);

            int j = 0, k = cache.Length;
            for (int i = 0; i < span.Length; i++)
            {
                ref V item = ref span.Ref(i);
                if (Extract(in item) is null)
                {
                    cache.Ref(--k) = item;
                }
                else
                {
                    cache.Ref(j) = item;
                    j++;
                }
            }
            Ensure(j == k);
            k = span.Length - j;
            var sub = cache.Sub(j, cache.Length);
            sub.Reverse();
            sub.CopyTo(span);
            cache.Sub(0, j).CopyTo(span.Sub(k, span.Length));
            return k;
        }
    }
}
