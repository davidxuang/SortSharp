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

internal abstract partial class SortBase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static bool Ensure([DoesNotReturnIf(false)] bool condition)
        => condition || ThrowInvariantViolated();
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ThrowInvariantViolated()
        => throw new InvalidOperationException("The ordering invariant was violated. This may be caused by an inconsistent comparer or concurrent modification.");

#if NET7_0_OR_GREATER
    [Template(nameof(T), null, nameof(span))]
    internal static int MoveNansToFront<T>(Span<T> span)
        where T : unmanaged, IFloatingPointIeee754<T>
    {
        ref T first = ref span.Ref(0);
        ref T swap = ref span.Ref(0);
        ref T last = ref span.Ref(span.Length);
        while (!Unsafe.AreSame(ref first, ref last) && !T.IsNaN(first))
            first = ref Unsafe.Inc(ref first);
        for (; !Unsafe.AreSame(ref first, ref last); first = ref Unsafe.Add(ref first, 1))
        {
            if (T.IsNaN(first))
            {
                Swap(ref first, ref swap);
                swap = ref Unsafe.Add(ref swap, 1);
            }
        }
        return span.Offset(in swap);
    }
#else
    [Template(nameof(T), null, nameof(span))]
    internal static int MoveNansToFront<T>(Span<T> span)
        where T : unmanaged
    {
        Debug.Assert(typeof(T) == typeof(double) || typeof(T) == typeof(float)
#if NETSTANDARD2_0_OR_GREATER || NET461_OR_GREATER || NETCOREAPP2_0_OR_GREATER
            || typeof(T) == typeof(Half)
#endif
            );

        ref T first = ref span.Ref(0);
        ref T swap = ref span.Ref(0);
        ref T last = ref span.Ref(span.Length);
        for (; !Unsafe.AreSame(ref first, ref last); first = ref Unsafe.Add(ref first, 1))
        {
            if ((typeof(T) == typeof(double) && double.IsNaN((double)(object)first))
                || (typeof(T) == typeof(float) && float.IsNaN((float)(object)first))
#if NETSTANDARD2_0_OR_GREATER || NET461_OR_GREATER || NETCOREAPP2_0_OR_GREATER
                || (typeof(T) == typeof(Half) && Half.IsNaN((Half)(object)first))
#endif
                )
            {
                Swap(ref first, ref swap);
                swap = ref Unsafe.Add(ref swap, 1);
            }
        }
        return span.Offset(in swap);
    }
#endif

    internal abstract partial class Fn<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static bool Less(ref readonly T a, ref readonly T b, Comparison<T> comp)
            => comp(a, b) < 0;
    }

    /// <summary>
    /// Provides sorting implementations that use a specialized less-than operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For IEEE 754 floating-point types, all NaN values must be removed or
    /// partitioned out of the range before invoking an algorithm in this specialization.
    /// </para>
    /// <para>
    /// The less-than operation used here does not provide a total order for NaN values.
    /// </para>
    /// </remarks>
    internal abstract partial class Op<T>
#if NET7_0_OR_GREATER
        where T : unmanaged, IComparisonOperators<T, T, bool>
#else
        where T : unmanaged, IComparable<T>
#endif
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static bool Less(ref readonly T a, ref readonly T b)
#if NET7_0_OR_GREATER
            => a < b;
#else
        {
            if (typeof(T) == typeof(byte)) return (byte)(object)a < (byte)(object)b;
            if (typeof(T) == typeof(sbyte)) return (sbyte)(object)a < (sbyte)(object)b;
            if (typeof(T) == typeof(ushort)) return (ushort)(object)a < (ushort)(object)b;
            if (typeof(T) == typeof(short)) return (short)(object)a < (short)(object)b;
            if (typeof(T) == typeof(uint)) return (uint)(object)a < (uint)(object)b;
            if (typeof(T) == typeof(int)) return (int)(object)a < (int)(object)b;
            if (typeof(T) == typeof(ulong)) return (ulong)(object)a < (ulong)(object)b;
            if (typeof(T) == typeof(long)) return (long)(object)a < (long)(object)b;
            if (typeof(T) == typeof(nuint)) return (nuint)(object)a < (nuint)(object)b;
            if (typeof(T) == typeof(nint)) return (nint)(object)a < (nint)(object)b;
            if (typeof(T) == typeof(float)) return (float)(object)a < (float)(object)b;
            if (typeof(T) == typeof(double)) return (double)(object)a < (double)(object)b;
#if NETSTANDARD2_0_OR_GREATER || NET461_OR_GREATER || NETCOREAPP2_0_OR_GREATER
            if (typeof(T) == typeof(Half)) return (Half)(object)a < (Half)(object)b;
#endif
            return a.CompareTo(b) < 0;
        }
#endif
        }

        internal abstract partial class Cmp<T>
        where T : IComparable<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static bool Less(ref readonly T a, ref readonly T b)
        {
            if (typeof(T) == typeof(byte)) return (byte)(object)a < (byte)(object)b;
            if (typeof(T) == typeof(sbyte)) return (sbyte)(object)a < (sbyte)(object)b;
            if (typeof(T) == typeof(ushort)) return (ushort)(object)a < (ushort)(object)b;
            if (typeof(T) == typeof(short)) return (short)(object)a < (short)(object)b;
            if (typeof(T) == typeof(uint)) return (uint)(object)a < (uint)(object)b;
            if (typeof(T) == typeof(int)) return (int)(object)a < (int)(object)b;
            if (typeof(T) == typeof(ulong)) return (ulong)(object)a < (ulong)(object)b;
            if (typeof(T) == typeof(long)) return (long)(object)a < (long)(object)b;
            if (typeof(T) == typeof(nuint)) return (nuint)(object)a < (nuint)(object)b;
            if (typeof(T) == typeof(nint)) return (nint)(object)a < (nint)(object)b;
            if (typeof(T) == typeof(float)) return (float)(object)a < (float)(object)b || (!((float)(object)a >= (float)(object)b) && !float.IsNaN((float)(object)b));
            if (typeof(T) == typeof(double)) return (double)(object)a < (double)(object)b || (!((double)(object)a >= (double)(object)b) && !double.IsNaN((double)(object)b));
#if NETSTANDARD2_0_OR_GREATER || NET461_OR_GREATER || NETCOREAPP2_0_OR_GREATER
            if (typeof(T) == typeof(Half)) return (Half)(object)a < (Half)(object)b || (!((Half)(object)a >= (Half)(object)b) && !Half.IsNaN((Half)(object)b));
#endif
            return a is not null
                ? a.CompareTo(b) < 0
                : b is not null;
        }
    }

    internal abstract partial class Cmp<T, C>
        where C : IComparer<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static bool Less(ref readonly T a, ref readonly T b, C comp)
            => comp.Compare(a, b) < 0;
    }

    internal abstract partial class Fn<T>
    {
        [Template(nameof(T), nameof(comp))]
        protected static int LowerBound(ReadOnlySpan<T> span, in T value, Comparison<T> comp)
        {
            int i = 0;
            int count = span.Length;
            while (count > 0)
            {
                int step = count / 2;
                int j = i + step;

                if (Less(in span.Ref(j), in value, comp))
                {
                    i = j + 1;
                    count -= step + 1;
                }
                else
                {
                    count = step;
                }
            }
            return i;
        }

        [Template(nameof(T), nameof(comp))]
        protected static int UpperBound(ReadOnlySpan<T> span, in T value, Comparison<T> comp)
        {
            int i = 0;
            int count = span.Length;
            while (count > 0)
            {
                int step = count / 2;
                int j = i + step;

                if (!Less(in value, in span.Ref(j), comp))
                {
                    i = j + 1;
                    count -= step + 1;
                }
                else
                {
                    count = step;
                }
            }
            return i;
        }

        [Template(nameof(T), nameof(comp), nameof(a), nameof(b),
            Switch = TemplateVariants.IComparable | TemplateVariants.IComparisonOperators)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void Sort2U(ref T a, ref T b, Comparison<T> comp)
        {
            if (Less(in b, in a, comp))
            {
                Swap(ref a, ref b);
            }
        }
    }

    internal abstract partial class Op<T>
    {
        [Template(nameof(T), null, nameof(a), nameof(b))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void Sort2U(ref T a, ref T b)
        {
            T x = a;
            T y = b;
            bool m = Less(in y, in x);
            a = m ? y : x;
            b = m ? x : y;
        }
    }

    internal abstract partial class Cmp<T>
        where T : IComparable<T>
    {
        [Template(nameof(T), null, nameof(a), nameof(b))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void Sort2U(ref T a, ref T b)
        {
            T x = a;
            T y = b;
            bool m = Less(in y, in x);
            a = m ? y : x;
            b = m ? x : y;
        }
    }

    internal abstract partial class Fn<T>
    {
        // Sorts the elements *a, *b and *c using comparison function comp.
        [Template(nameof(T), nameof(comp), nameof(a), nameof(b), nameof(c))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void Sort3U(ref T a, ref T b, ref T c, Comparison<T> comp)
        {
            Sort2U(ref a, ref b, comp);
            Sort2U(ref b, ref c, comp);
            Sort2U(ref a, ref b, comp);
        }

        [Template(nameof(T), nameof(comp), nameof(span), nameof(target))]
        internal static void Merge(ReadOnlySpan<T> span, int split, Span<T> target, Comparison<T> comp)
        {
            Debug.Assert(split > 0 && split < span.Length);

            ref readonly T indxA = ref span.Ref(0);
            ref readonly T lastA = ref span.Ref(split);
            ref readonly T indxB = ref lastA;
            ref readonly T lastB = ref span.Ref(span.Length);
            ref T insert = ref target.Ref(0);

            while (true)
            {
                if (!Less(in indxB, in indxA, comp))
                {
                    insert = indxA;
                    indxA = ref Unsafe.RoInc(in indxA);
                    insert = ref Unsafe.Inc(ref insert);
                    if (Unsafe.AreSame(in indxA, in lastA))
                    {
                        int i = span.Offset(in indxB), j = target.Offset(in insert);
                        span.Sub(i, span.Length).CopyTo(target.Sub(j, target.Length));
                        break;
                    }
                }
                else
                {
                    insert = indxB;
                    indxB = ref Unsafe.RoInc(in indxB);
                    insert = ref Unsafe.Inc(ref insert);
                    if (Unsafe.AreSame(in indxB, in lastB))
                    {
                        int i = span.Offset(in indxA), j = target.Offset(in insert);
                        span.Sub(i, split).CopyTo(target.Sub(j, target.Length));
                        break;
                    }
                }
            }
        }

        [Template(nameof(T), nameof(comp), nameof(span))]
        protected static void HeapSort(Span<T> span, Comparison<T> comp)
        {
            if (span.Length == 0) return;
            int n = span.Length;
            for (int i = n >> 1; i >= 1; i--)
            {
                DownHeap(span, i, n, comp);
            }

            for (int i = n; i > 1; i--)
            {
                Swap(ref span.Ref(0), ref span.Ref(i - 1));
                DownHeap(span, 1, i - 1, comp);
            }
        }

        [Template(nameof(T), nameof(comp), nameof(span))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void DownHeap(Span<T> span, int i, int n, Comparison<T> comp)
        {
            T d = span.Ref(i - 1);
            while (i <= n >> 1)
            {
                int child = 2 * i;
                if (child < n)
                {
                    child += Convert.ToInt32(Less(in span.Ref(child - 1), in span.Ref(child), comp));
                }

                if (!Less(in d, in span.Ref(child - 1), comp))
                    break;

                span.Ref(i - 1) = span.Ref(child - 1);
                i = child;
            }

            span.Ref(i - 1) = d;
        }

        // Sorts the range of the given length beginning at first using insertion sort with the given
        // comparison function.
        [Template(nameof(T), nameof(comp), nameof(first))]
        protected static void InsertionSort(ref T first, int length, Comparison<T> comp, int offset = 0)
        {
            if ((length -= offset) <= 1) return;

            ref T curr = ref Unsafe.Add(ref first, offset);
            while (--length != 0)
            {
                curr = ref Unsafe.Inc(ref curr);
                ref T sift = ref curr;
                ref T sift1 = ref Unsafe.Dec(ref curr);

                // Compare first so we can avoid 2 moves for an element already positioned correctly.
                if (Less(in sift, in sift1, comp))
                {
                    T tmp = sift;
                    do
                    {
                        sift = sift1;
                        sift = ref Unsafe.Dec(ref sift);

                        if (Unsafe.AreSame(ref sift, ref first)) break;
                        sift1 = ref Unsafe.Dec(ref sift1);
                    }
                    while (Less(in tmp, in sift1, comp));

                    sift = tmp;
                }
            }
        }
    }
}

internal struct Range(int start, int end)
{
    public int Start = start;
    public int End = end;

    public readonly int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => End - Start;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Deconstruct(out int start, out int end)
    {
        start = Start;
        end = End;
    }

    public readonly override string ToString()
    {
        return $"[{Start}, {End})";
    }
}
