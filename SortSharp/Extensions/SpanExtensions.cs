using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SortSharp.Extensions;

internal static partial class SpanExtensions
{
    extension(Unsafe)
    {
#if !NET8_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullRef<T>(ref readonly T source)
            => Unsafe.IsNullRef(ref Unsafe.AsRef(in source));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint ByteOffset<T>(ref readonly T origin, ref readonly T target)
            => Unsafe.ByteOffset(ref Unsafe.AsRef(in origin), ref Unsafe.AsRef(in target));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreSame<T>(ref readonly T left, ref readonly T right)
            => Unsafe.AreSame(ref Unsafe.AsRef(in left), ref Unsafe.AsRef(in right));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAddressLessThan<T>(ref readonly T left, ref readonly T right)
            => Unsafe.IsAddressLessThan(ref Unsafe.AsRef(in left), ref Unsafe.AsRef(in right));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAddressGreaterThan<T>(ref readonly T left, ref readonly T right)
            => Unsafe.IsAddressGreaterThan(ref Unsafe.AsRef(in left), ref Unsafe.AsRef(in right));
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int Offset<T>(ref readonly T a, ref readonly T b)
            => (int)Unsafe.ByteOffset(in a, in b) / Unsafe.SizeOf<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref T Next<T>(ref T source)
            => ref Unsafe.Add(ref source, 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref T Prev<T>(ref T source)
            => ref Unsafe.Subtract(ref source, 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref readonly T RONext<T>(ref readonly T source)
            => ref Unsafe.Add(ref Unsafe.AsRef(in source), 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref readonly T ROPrev<T>(ref readonly T source)
            => ref Unsafe.Subtract(ref Unsafe.AsRef(in source), 1);
            
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref readonly T ROAdd<T>(ref readonly T source, int elementOffset)
            => ref Unsafe.Add(ref Unsafe.AsRef(in source), elementOffset);
    }

#if !NET8_0_OR_GREATER
    extension(MemoryMarshal)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<T> CreateReadOnlySpan<T>(ref readonly T reference, int length)
            => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in reference), length);
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Offset<T>(this scoped ReadOnlySpan<T> span, ref readonly T value)
        => (int)Unsafe.ByteOffset(in MemoryMarshal.GetReference(span), in value) / Unsafe.SizeOf<T>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref T Ref<T>(this Span<T> span, int index)
#if DEBUG
        => ref (index != -1 && index != span.Length // allows on-bound access
            ? ref span[index]
            : ref Unsafe.Add(ref MemoryMarshal.GetReference(span), index));
#else
        => ref Unsafe.Add(ref MemoryMarshal.GetReference(span), index);
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ref readonly T Ref<T>(this ReadOnlySpan<T> span, int index)
#if DEBUG
        => ref ((index != span.Length)
            ? ref span[index]
            : ref Unsafe.Add(ref MemoryMarshal.GetReference(span), index));
#else
        => ref Unsafe.Add(ref MemoryMarshal.GetReference(span), index);
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Span<T> Sub<T>(this Span<T> span, Range range)
#if DEBUG || !(NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER)
        => span.Slice(range.Start, range.Length);
#else
        => MemoryMarshal.CreateSpan(ref span.Ref(range.Start), range.Length);
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<T> Sub<T>(this ReadOnlySpan<T> span, Range range)
#if DEBUG || !(NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER)
        => span.Slice(range.Start, range.Length);
#else
        => MemoryMarshal.CreateReadOnlySpan(in span.Ref(range.Start), range.Length);
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Span<T> Sub<T>(this Span<T> span, int start, int end)
#if DEBUG || !(NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER)
        => span.Slice(start, end - start);
#else
        => MemoryMarshal.CreateSpan(ref span.Ref(start), end - start);
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<T> Sub<T>(this ReadOnlySpan<T> span, int start, int end)
#if DEBUG || !(NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER)
        => span.Slice(start, end - start);
#else
        => MemoryMarshal.CreateReadOnlySpan(in span.Ref(start), end - start);
#endif

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
            a = ref Unsafe.Next(ref a);
            b = ref Unsafe.Next(ref b);
        }
    }

    /// <see cref="https://github.com/scandum/rotate"/>
    internal static void Rotate<T>(Span<T> span, int left)
    {
        Debug.Assert(0 <= left && left <= span.Length);

        int right = span.Length - left;
        if (left == 0 || right == 0)
            return; // 0

        ref T a = ref span.Ref(0);
        ref T b = ref span.Ref(left);
        ref T c = ref b;
        ref T d = ref span.Ref(span.Length);
        T swap;
        int loop;

        if (left > right)
        {
            for (loop = right / 2; loop != 0; loop--)
            {
                b = ref Unsafe.Prev(ref b);
                swap = b;
                b = a;
                a = c;
                a = ref Unsafe.Next(ref a);
                d = ref Unsafe.Prev(ref d);
                c = d;
                c = ref Unsafe.Next(ref c);
                d = swap;
            }
            for (loop = Unsafe.Offset(ref a, ref b) / 2; loop != 0; loop--)
            {
                b = ref Unsafe.Prev(ref b);
                swap = b;
                b = a;
                d = ref Unsafe.Prev(ref d);
                a = d;
                a = ref Unsafe.Next(ref a);
                d = swap;
            }
        }
        else if (left < right)
        {
            for (loop = left / 2; loop != 0; loop--)
            {
                b = ref Unsafe.Prev(ref b);
                swap = b;
                b = a;
                a = c;
                a = ref Unsafe.Next(ref a);
                d = ref Unsafe.Prev(ref d);
                c = d;
                c = ref Unsafe.Next(ref c);
                d = swap;
            }
            for (loop = Unsafe.Offset(ref c, ref d) / 2; loop != 0; loop--)
            {
                swap = c;
                d = ref Unsafe.Prev(ref d);
                c = d;
                c = ref Unsafe.Next(ref c);
                d = a;
                a = swap;
                a = ref Unsafe.Next(ref a);
            }
        }
        else
        {
            for (loop = left; loop != 0; loop--)
            {
                swap = a;
                a = b;
                a = ref Unsafe.Next(ref a);
                b = swap;
                b = ref Unsafe.Next(ref b);
            }
            return; // !!!
        }

        for (loop = Unsafe.Offset(ref a, ref d) / 2; loop != 0; loop--)
        {
            swap = a;
            d = ref Unsafe.Prev(ref d);
            a = d;
            a = ref Unsafe.Next(ref a);
            d = swap;
        }

        // return end - (mid - begin);
    }

    internal static void Rotate<T>(Span<T> span, int left, Span<T> cache)
    {
        Debug.Assert(0 <= left && left <= span.Length);

        int right = span.Length - left;

        if (left == 0 || right == 0)
            return;
        
        if (left <= right)
        {
            if (left <= cache.Length)
            {
                span.Sub(0, left).CopyTo(cache);
                span.Sub(left, span.Length).CopyTo(span.Sub(0, right));
                cache.Sub(0, left).CopyTo(span.Sub(right, span.Length));
                return;
            }
        }
        else
        {
            if (right <= cache.Length)
            {
                span.Sub(left, span.Length).CopyTo(cache);
                span.Sub(0, left).CopyTo(span.Sub(right, span.Length));
                cache.Sub(0, right).CopyTo(span.Sub(0, right));
                return;
            }
        }

        Rotate(span, left);
    }
}
