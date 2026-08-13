using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SortSharp.Compat;

namespace SortSharp;

public static partial class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Rotate<T>(this Span<T> span, int left, Span<T> cache = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(left, 0, nameof(left));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(left, span.Length, nameof(left));

        SpanExtensions.Rotate(span, left, cache);
    }
}

internal static partial class SpanExtensions
{
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref readonly T RoInc<T>(ref readonly T source)
            => ref Unsafe.Add(ref Unsafe.AsRef(in source), 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref readonly T RoDec<T>(ref readonly T source)
            => ref Unsafe.Subtract(ref Unsafe.AsRef(in source), 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref readonly T RoAdd<T>(ref readonly T source, int elementOffset)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Swap<T>(ref T a, ref T b)
    {
        T t = a;
        a = b;
        b = t;
    }

    internal static void SwapBlock<T>(ref T a, ref T b, int length)
    {
        while (length-- > 0)
        {
            Swap(ref a, ref b);
            a = ref Unsafe.Inc(ref a);
            b = ref Unsafe.Inc(ref b);
        }
    }

    /// <see cref="https://github.com/scandum/rotate"/>
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
}
