using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using SortSharp.Compat;
using SortSharp.Foundation;
using SortSharp.SourceGenerators;

namespace SortSharp;

public static partial class Extensions
{
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixLsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="Radix_TotalOrderIeee754"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    [ApiTemplate("double")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixLsdSort(this Span<double> span)
    {
        Radix.BFP<double>.LsdSort(span);
    }

    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixMsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="Radix_TotalOrderIeee754"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="profile"]/*' />
    [ApiTemplate("double")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixMsdSort(this Span<double> span, MemoryProfile profile = MemoryProfile.Baseline)
    {
        Radix.BFP<double>.MsdSort(span, profile);
    }

#if NET7_0_OR_GREATER
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixLsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="Radix_IBinaryFloatingPointIeee754"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/TypeParams/Member[@name="T"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    [ApiTemplate(nameof(T), KeySelector = true)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixLsdSort<T>(this Span<T> span)
        where T : unmanaged, IBinaryFloatingPointIeee754<T>, IMinMaxValue<T>
    {
        Radix.BFP<T>.LsdSort(span);
    }

    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixMsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="Radix_IBinaryFloatingPointIeee754"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/TypeParams/Member[@name="T"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="profile"]/*' />
    [ApiTemplate(nameof(T), KeySelector = true)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixMsdSort<T>(this Span<T> span, MemoryProfile profile = MemoryProfile.Baseline)
        where T : unmanaged, IBinaryFloatingPointIeee754<T>, IMinMaxValue<T>
    {
        Radix.BFP<T>.MsdSort(span, profile);
    }

#endif
}

internal static partial class Radix
{
    [Receiver(nameof(Radix))]
    internal static partial class BFP<T>
        where T : unmanaged,
#if NET7_0_OR_GREATER
        IBinaryFloatingPointIeee754<T>, IMinMaxValue<T>
#else
        IComparable<T>
#endif
    {
        static readonly TotalOrderIeee754Comparer<T> Comparer = new();
        static readonly int Width;

        static BFP()
        {
#if NET7_0_OR_GREATER
            int bits = 1 + T.MaxValue.GetSignificandBitLength() + T.PositiveInfinity.GetExponentShortestBitLength();
            int quo = Math.DivRem(bits, 8, out int rem);
            Width = quo switch
            {
                _ when quo > Unsafe.SizeOf<T>() => 0, // NotSupportedException
                >= 1 => quo,
                0 when rem > 0 => 1, // FP4?
                _ => 0, // NotSupportedException
            };
#else
            Width = Unsafe.SizeOf<T>();
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Compare(ref readonly T a, ref readonly T b) => Comparer.Compare(a, b);

        [ImplTemplate(nameof(T), nameof(span))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void FallbackSort(Span<T> span)
        {
            PDQ.Cmp<T, TotalOrderIeee754Comparer<T>>.Sort(span, Comparer);
        }

        [ImplTemplate(nameof(T), nameof(span))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void FallbackSortStable(Span<T> span, MemoryProfile profile)
        {
            if (span.Length < 32)
                IPN.Cmp<T, TotalOrderIeee754Comparer<T>>.SmallSort(span, Comparer);
            else
                Wiki.Cmp<T, TotalOrderIeee754Comparer<T>>.Sort(span, Comparer, profile);
        }

        [ImplTemplate(nameof(T), nameof(span), nameof(cache))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void FallbackSortStableReuse(Span<T> span, Span<T> cache)
        {
            if (span.Length < 32)
                IPN.Cmp<T, TotalOrderIeee754Comparer<T>>.SmallSort(span, Comparer);
            else
            {
                var iterator = new Wiki.Iterator(span.Length, 4);
                Wiki.Cmp<T, TotalOrderIeee754Comparer<T>>.BlockSortLoop(span, Comparer, ref iterator);
                Wiki.Cmp<T, TotalOrderIeee754Comparer<T>>.SortLoop(span, cache, Comparer, ref iterator);
            }
        }

        private ref struct State(int offset) : IState
        {
            public int Offset { get; private set; } = offset;
#if NET7_0_OR_GREATER
            public static State CreateLsd() => new(BitConverter.IsLittleEndian ? 0 : Width - 1);
            public static State CreateMsd() => new(BitConverter.IsLittleEndian ? Width - 1 : 0);
#else
            public static State CreateLsd() => new(0);
            public static State CreateMsd() => new(Width - 1);
#endif

            public readonly int BucketCount => 1 << 8;

            public bool TryMoveFromLsd()
            {
#if NET7_0_OR_GREATER
                if (BitConverter.IsLittleEndian)
#endif
                    return ++Offset < Width;
#if NET7_0_OR_GREATER
                else
                    return --Offset >= 0;
#endif
            }
            public bool TryMoveFromMsd()
            {
#if NET7_0_OR_GREATER
                if (BitConverter.IsLittleEndian)
#endif
                    return --Offset >= 0;
#if NET7_0_OR_GREATER
                else
                    return ++Offset < Width;
#endif
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Extract(ref readonly T value, ref readonly State state)
        {
#if NET7_0_OR_GREATER
            T v = T.IsNegative(value) ? ~value : -value;
            return Unsafe.Add(ref Unsafe.As<T, byte>(ref v), state.Offset);
#else
            if (value is double d)
            {
                long i = BitConverter.DoubleToInt64Bits(d);
                return (int)(((i < 0 ? ~i : i ^ long.MinValue) >>> (state.Offset * 8)) & 0xff);
            }
            else if (value is float f)
            {
                int i = BitConverter.SingleToInt32Bits(f);
                return ((i < 0 ? ~i : i ^ int.MinValue) >>> (state.Offset * 8)) & 0xff;
            }
#if NETSTANDARD2_0_COMPAT
            else if (value is Half h)
            {
                short i = BitConverter.HalfToInt16Bits(h);
                return ((i < 0 ? ~i : i ^ short.MinValue) >>> (state.Offset * 8)) & 0xff;
            }
#endif
            ThrowHelper.ThrowUnreachable();
            return -1; // unreachable
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool MsdSkipBucket(int _) => false;

        [ImplTemplate(nameof(T), nameof(span))]
        [SkipLocalsInit]
        internal static void LsdSort(Span<T> span)
        {
            if (span.Length > FallbackSortThreshold)
            {
                var state = State.CreateLsd();
                using MemoryOwner<T> owner = new(MemoryPool<T>.Shared.Rent(span.Length));
                LsdSortLoop(span, owner.Memory.Span.Sub(0, span.Length), stackalloc int[state.BucketCount + 1], state);
            }
            else if (span.Length > 1)
                FallbackSortStable(span, MemoryProfile.High);
        }

        [ImplTemplate(nameof(T), nameof(span))]
        [SkipLocalsInit]
        internal static void MsdSort(Span<T> span, MemoryProfile profile)
        {
            if (span.Length > FallbackSortThreshold)
            {
                var state = State.CreateMsd();
                Span<int> sums = stackalloc int[state.BucketCount + 1];
                if (profile < MemoryProfile.High) // unstable
                    MsdSortUnstable(span, sums, state, profile);
                else
                {
                    using MemoryOwner<T> owner = new(MemoryPool<T>.Shared.Rent(span.Length));
                    MsdSortBuffered(span, owner.Memory.Span.Sub(0, span.Length), sums, state, true);
                }
            }
            else if (span.Length > 1)
            {
                if (profile < MemoryProfile.High) // unstable
                    FallbackSort(span);
                else
                    FallbackSortStable(span, profile);
            }
        }

        internal static partial class From<V, TSelector>
            where TSelector :
#if !NET7_0_OR_GREATER
                unmanaged,
#endif
                IKeySelector<V, T>
        {
            private static readonly KeySelectorComparer<V, T, TSelector, TotalOrderIeee754Comparer<T>> Comparer = new(BFP<T>.Comparer);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static int Compare(ref readonly V a, ref readonly V b) => Comparer.Compare(a, b);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static int Extract(ref readonly V value, ref readonly State state)
            {
#if NET7_0_OR_GREATER
                T key = TSelector.Select(in value);
#else
                T key = default(TSelector).SelectInst(in value);
#endif
                return BFP<T>.Extract(in key, in state);
            }
        }
    }
}
