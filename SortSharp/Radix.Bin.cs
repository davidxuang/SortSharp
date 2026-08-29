using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SortSharp.Foundation;
using SortSharp.SourceGenerators;

namespace SortSharp;

/// <summary>
/// Provides extension methods for <see cref="Span{T}"/> of <see cref="Guid"/>.
/// </summary>
public static partial class GuidExtensions
{
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixLsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    [ApiTemplate(nameof(Guid), KeySelector = true)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixLsdSort(this Span<Guid> span)
    {
        Radix.Bin<Guid>.LsdSort(span, ByteOrder.Native);
    }

    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixMsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="profile"]/*' />
    [ApiTemplate(nameof(Guid), KeySelector = true)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixMsdSort(this Span<Guid> span, MemoryProfile profile = MemoryProfile.Baseline)
    {
        Radix.Bin<Guid>.MsdSort(span, ByteOrder.Native, profile);
    }
}

internal enum ByteOrder : byte
{
    Native,
    LittleEndian,
    BigEndian,
}

internal static partial class Radix
{
    [Receiver(nameof(Radix))]
    internal static partial class Bin<T>
        where T : unmanaged, IComparable<T>
    {
        static readonly int Width = Unsafe.SizeOf<T>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Compare(ref readonly T a, ref readonly T b)
            => a.CompareTo(b);

        [ImplTemplate(nameof(T), nameof(span))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void FallbackSort(Span<T> span)
        {
            PDQ.Cmp<T>.Sort(span);
        }

        [ImplTemplate(nameof(T), nameof(span))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void FallbackSortStable(Span<T> span, MemoryProfile profile)
        {
            if (span.Length < 32)
                IPN.Cmp<T>.SmallSort(span);
            else
                Wiki.Cmp<T>.Sort(span, profile);
        }

        [ImplTemplate(nameof(T), nameof(span), nameof(cache))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void FallbackSortStableReuse(Span<T> span, Span<T> cache)
        {
            if (span.Length < 32)
                IPN.Cmp<T>.SmallSort(span);
            else
            {
                var iterator = new Wiki.Iterator(span.Length, 4);
                Wiki.Cmp<T>.BlockSortLoop(span, ref iterator);
                Wiki.Cmp<T>.SortLoop(span, cache, ref iterator);
            }
        }

        private static int GetOffset(ByteOrder order, int index)
            => order switch
            {
                ByteOrder.Native when typeof(T) == typeof(Guid)
                    => !BitConverter.IsLittleEndian ? Width - 1 - index : index switch
                    {
                        >= 12 => (Width - 1 - index) ^ 3,
                        >= 8 => (Width - 1 - index) ^ 1,
                        _ => Width - 1 - index
                    },
                ByteOrder.Native => GetOffset(BitConverter.IsLittleEndian ? ByteOrder.LittleEndian : ByteOrder.BigEndian, index),
                ByteOrder.LittleEndian => index,
                ByteOrder.BigEndian => Width - 1 - index,
                _ => ThrowHelper.ThrowUnreachable(),
            };

        [StructLayout(LayoutKind.Auto)]
        private ref struct State : IState
        {
            private ByteOrder Order { get; init; }
            private int Index { get; set { field = value; Offset = (short)GetOffset(Order, value); } }
            public short Offset { get; private set; }

            public static State CreateLsd(ByteOrder order) => new() { Order = order, Index = 0 };
            public static State CreateMsd(ByteOrder order) => new() { Order = order, Index = Width - 1 };
            public readonly int BucketCount => 1 << 8;
            public bool TryMoveFromLsd() => ++Index < Width;
            public bool TryMoveFromMsd() => --Index >= 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Extract(ref readonly T value, ref readonly State state)
            => Unsafe.RO.Add(in Unsafe.RO.As<T, byte>(in value), state.Offset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool MsdSkipBucket(int _) => false;

        [ImplTemplate(nameof(T), nameof(span))]
        [SkipLocalsInit]
        internal static void LsdSort(Span<T> span, ByteOrder order)
        {
            if (span.Length > FallbackSortThreshold)
            {
                var state = State.CreateLsd(order);
                using MemoryOwner<T> owner = new(MemoryPool<T>.Shared.Rent(span.Length));
                LsdSortLoop(span, owner.Memory.Span.Sub(0, span.Length), stackalloc int[state.BucketCount + 1], state);
            }
            else if (span.Length > 1)
                FallbackSortStable(span, MemoryProfile.High);
        }

        [ImplTemplate(nameof(T), nameof(span))]
        [SkipLocalsInit]
        internal static void MsdSort(Span<T> span, ByteOrder order, MemoryProfile profile)
        {
            if (span.Length > FallbackSortThreshold)
            {
                var state = State.CreateMsd(order);
                if (profile < MemoryProfile.High) // unstable
                    MsdSortUnstable(span, stackalloc int[state.BucketCount + 1], state, profile);
                else
                {
                    using MemoryOwner<T> owner = new(MemoryPool<T>.Shared.Rent(span.Length));
                    MsdSortBuffered(span, owner.Memory.Span.Sub(0, span.Length), stackalloc int[state.BucketCount + 1], state, true);
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
            private static readonly KeySelectorComparer<V, T, TSelector> Comparer = new();

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
                return Bin<T>.Extract(in key, in state);
            }
        }
    }
}
