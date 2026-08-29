using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using SortSharp.Compat;
using SortSharp.Foundation;
using SortSharp.SourceGenerators;
using static SortSharp.SpanOperations;

namespace SortSharp;

public static partial class Extensions
{
#if NET7_0_OR_GREATER
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="LowerBound"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/TypeParams/Member[@name="T"]/*' />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LowerBound<T>(this ReadOnlySpan<T> span, in T value)
        where T : unmanaged, IComparisonOperators<T, T, bool>
        => ComparisonOperations.Op<T>.LowerBound(span, value);
        
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="UpperBound"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/TypeParams/Member[@name="T"]/*' />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int UpperBound<T>(this ReadOnlySpan<T> span, in T value)
        where T : unmanaged, IComparisonOperators<T, T, bool>
        => ComparisonOperations.Op<T>.UpperBound(span, value);
#endif

    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="LowerBound"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/TypeParams/Member[@name="T"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="comparer"]/*' />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LowerBound<T>(this ReadOnlySpan<T> span, in T value, IComparer<T>? comparer = null)
        => LowerBound(span, value, comparer);

    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="UpperBound"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/TypeParams/Member[@name="T"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="comparer"]/*' />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int UpperBound<T>(this ReadOnlySpan<T> span, in T value, IComparer<T>? comparer = null)
        => UpperBound(span, value, comparer);

    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="LowerBound"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/TypeParams/Member[@name="T"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="comparison"]/*' />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LowerBound<T>(this ReadOnlySpan<T> span, in T value, Comparison<T> comparison)
        => ComparisonOperations.Fn<T>.LowerBound(span, value, comparison);

    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="UpperBound"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/TypeParams/Member[@name="T"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="comparison"]/*' />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int UpperBound<T>(this ReadOnlySpan<T> span, in T value, Comparison<T> comparison)
        => ComparisonOperations.Fn<T>.UpperBound(span, value, comparison);

    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="LowerBound"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/TypeParams/Member[@name="T_TComparer"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="comparer"]/*' />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LowerBound<T, TComparer>(this ReadOnlySpan<T> span, in T value, TComparer comparer)
        where TComparer : IComparer<T>?
    {
        if (span.Length <= 1)
            return 0;
        else if (typeof(TComparer).IsValueType)
#pragma warning disable CS8631
            return ComparisonOperations.Cmp<T, TComparer>.LowerBound(span, value, comparer);
#pragma warning restore CS8631
        else if (comparer is null || comparer as IComparer<T> == Comparer<T>.Default)
            return Dispatcher<T>.To.LowerBound(span, value);
        else
            return ComparisonOperations.Cmp<T, IComparer<T>>.LowerBound(span, value, comparer);
    }

    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="UpperBound"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/TypeParams/Member[@name="T_TComparer"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="comparer"]/*' />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int UpperBound<T, TComparer>(this ReadOnlySpan<T> span, in T value, TComparer comparer)
        where TComparer : IComparer<T>?
    {
        if (span.Length <= 1)
            return 0;
        else if (typeof(TComparer).IsValueType)
#pragma warning disable CS8631
            return ComparisonOperations.Cmp<T, TComparer>.UpperBound(span, value, comparer);
#pragma warning restore CS8631
        else if (comparer is null || comparer as IComparer<T> == Comparer<T>.Default)
            return Dispatcher<T>.To.UpperBound(span, value);
        else
            return ComparisonOperations.Cmp<T, IComparer<T>>.UpperBound(span, value, comparer);
    }
}

internal static partial class ComparisonOperations
{
    internal abstract partial class Fn<T>
    {
        [ImplTemplate(nameof(T), Comparer = nameof(comp), Disable = ComparerOverloads.All)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static bool Less(ref readonly T a, ref readonly T b, Comparison<T> comp)
            => comp(a, b) < 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static int Compare(ref readonly T a, ref readonly T b, Comparison<T> comp)
            => comp(a, b);

        [ImplTemplate(nameof(T), nameof(a), nameof(b), Comparer = nameof(comp),
            Disable = ComparerOverloads.IComparable | ComparerOverloads.IComparisonOperators)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void CmpEx(ref T a, ref T b, Comparison<T> comp)
        {
            if (Less(in b, in a, comp))
            {
                Swap(ref a, ref b);
            }
        }
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
#if NETSTANDARD2_0_COMPAT
            if (typeof(T) == typeof(Half)) return (Half)(object)a < (Half)(object)b;
#endif
            return a.CompareTo(b) < 0;
        }
#endif

        [ImplTemplate(nameof(T), nameof(a), nameof(b))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void CmpEx(ref T a, ref T b)
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
#if NETSTANDARD2_0_COMPAT
            if (typeof(T) == typeof(Half)) return (Half)(object)a < (Half)(object)b || (!((Half)(object)a >= (Half)(object)b) && !Half.IsNaN((Half)(object)b));
#endif
            return a is not null
                ? a.CompareTo(b) < 0
                : b is not null;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static int Compare(ref readonly T a, ref readonly T b)
            => a is not null
                ? a.CompareTo(b)
                : b is null ? 0 : -1;

        [ImplTemplate(nameof(T), nameof(a), nameof(b))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void CmpEx(ref T a, ref T b)
        {
            T x = a;
            T y = b;
            bool m = Less(in y, in x);
            a = m ? y : x;
            b = m ? x : y;
        }
    }

    internal abstract partial class Cmp<T, C>
        where C : IComparer<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static bool Less(ref readonly T a, ref readonly T b, C comp)
            => comp.Compare(a, b) < 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static int Compare(ref readonly T a, ref readonly T b, C comp)
            => comp.Compare(a, b);
    }

    internal abstract partial class Fn<T>
    {
        [ImplTemplate(nameof(T), Comparer = nameof(comp))]
        public static int LowerBound(ReadOnlySpan<T> span, in T value, Comparison<T> comp)
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

        [ImplTemplate(nameof(T), Comparer = nameof(comp))]
        public static int UpperBound(ReadOnlySpan<T> span, in T value, Comparison<T> comp)
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

        [ImplTemplate(nameof(T), nameof(span), nameof(target), Comparer = nameof(comp))]
        protected static void Merge(ReadOnlySpan<T> span, int split, Span<T> target, Comparison<T> comp)
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
                    indxA = ref Unsafe.RO.Inc(in indxA);
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
                    indxB = ref Unsafe.RO.Inc(in indxB);
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

        [ImplTemplate(nameof(T), nameof(span), Comparer = nameof(comp))]
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

        [ImplTemplate(nameof(T), nameof(span), Comparer = nameof(comp))]
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
        [ImplTemplate(nameof(T), nameof(first), Comparer = nameof(comp))]
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
