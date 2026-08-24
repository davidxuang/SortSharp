using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SortSharp.Compat;
using SortSharp.SourceGeneration;

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
    /// <param name="span">The span to rotate.</param>
    /// <param name="left">The number of positions to rotate left.</param>
    /// <param name="cache">Optional cache span to be used. It does not need to be cleared beforehand, and will not be cleared afterwards.</param>
    /// <remarks><see href="https://github.com/scandum/rotate"/></remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Rotate<T>(this Span<T> span, int left, Span<T> cache = default)
    {
        ArgumentException.ThrowIf(span.Overlaps(cache), "The cache span should not overlap with the input span.", nameof(cache));
        ArgumentOutOfRangeException.ThrowIfLessThan(left, 0, nameof(left));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(left, span.Length, nameof(left));

        SpanOperations.Rotate(span, left, cache);
    }
}

internal static partial class SpanOperations
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Ensure([DoesNotReturnIf(false)] bool condition)
        => condition || ThrowInvariantViolated();
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ThrowInvariantViolated()
        => throw new InvalidOperationException("The ordering invariant was violated. This may be caused by an inconsistent comparer or concurrent modification.");

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

    [OverloadTemplate(nameof(T), null)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Swap<T>(ref T a, ref T b)
    {
        T t = a;
        a = b;
        b = t;
    }

    [OverloadTemplate(nameof(T), null)]
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

    [OverloadTemplate(nameof(T), null)]
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

    [OverloadTemplate(nameof(T), null, nameof(_), Disable = DefaultOverloads.KeyValue)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SliceToLast<T>(ref Span<T> _) { }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SliceToLast<T, V>(ref Span<T> a, ref readonly Span<V> b)
    {
        if (b.Length < a.Length) a = a.Sub(0, b.Length);
    }

    [OverloadTemplate(nameof(T), null)]
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

    internal static void PrefixSums(Span<int> counts)
    {
        ref int last = ref counts.Ref(counts.Length - 1);
        for (ref int count = ref counts.Ref(0); Unsafe.IsAddressLessThan(ref count, ref last); count = ref Unsafe.Inc(ref count))
        {
            Unsafe.Inc(ref count) += count;
        }
    }


    [OverloadTemplate(nameof(T), null, nameof(span))]
    internal static int MoveNullsToFront<T>(Span<T> span)
        where T : class
    {
        ref T first = ref span.Ref(0);
        ref T swap = ref span.Ref(0);
        ref T last = ref span.Ref(span.Length);
#pragma warning disable CS8601
        while (!Unsafe.AreSame(in first, in last) && first is not null)
            first = ref Unsafe.Inc(ref first);
        for (; !Unsafe.AreSame(in first, in last); first = ref Unsafe.Inc(ref first))
        {
            if (first is null)
            {
                Swap(ref first, ref swap);
                swap = ref Unsafe.Inc(ref swap);
            }
        }
#pragma warning restore CS8601
        return span.Offset(in swap);
    }

    [OverloadTemplate(nameof(T), null, nameof(span), nameof(cache), Disable = DefaultOverloads.KeyValue)]
    internal static int MoveNullsToFrontStable<T>(Span<T> span, Span<T> cache)
        where T : class
    {
        Debug.Assert(span.Length == cache.Length);

        int j = 0;
        for (int i = 0; i < span.Length; i++)
        {
            ref T item = ref span.Ref(i);
            if (item is not null)
            {
                cache.Ref(j++) = item;
            }
        }
        int k = span.Length - j;
        span.Sub(0, k).Clear();
        cache.Sub(0, j).CopyTo(span.Sub(k, span.Length));
        return k;
    }

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
    [OverloadTemplate(nameof(T), null, nameof(span))]
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
    [OverloadTemplate(nameof(T), null, nameof(span))]
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
}
