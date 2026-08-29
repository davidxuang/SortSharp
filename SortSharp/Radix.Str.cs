using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SortSharp.Compat;
using SortSharp.Foundation;
using SortSharp.SourceGenerators;

namespace SortSharp;

public static partial class Extensions
{
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixLsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="StringOrdinal"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="profile"]/*' />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixMsdSort(this Span<string> span, MemoryProfile profile = MemoryProfile.Baseline)
    {
        Radix.Str.MsdSort(span, profile);
    }

    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixMsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="StringOrdinal"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/TypeParams/Member[@name="V"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="keys_items"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="profile"]/*' />
    public static void RadixMsdSort<V>(this Span<string> keys, Span<V> items, MemoryProfile profile = MemoryProfile.Baseline)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(keys.Length, items.Length, nameof(items));
        Radix.Str.MsdSort(keys, items, profile);
    }

#if NET7_0_OR_GREATER
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="RadixMsd"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Common/Member[@name="StringOrdinal"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/TypeParams/Member[@name="T_TSelector"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="span"]/*' />
    /// <include file='XmlDocComments.xml' path='XmlDocComments/Params/Member[@name="profile"]/*' />
    public static void RadixMsdSort<T, TSelector>(this Span<T> span, MemoryProfile profile = MemoryProfile.Baseline)
        where TSelector : IKeySelector<T, string>
    {
        Radix.Str.From<T, TSelector>.MsdSort(span, profile);
    }
#endif
}

internal partial class Radix
{
    [Receiver(nameof(Radix), Type = "string")]
    internal static partial class Str
    {
        static readonly StringComparer Comparer = StringComparer.Ordinal;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Compare(ref readonly string a, ref readonly string b) => Comparer.Compare(a, b);

        [ImplTemplate("string", nameof(span))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void FallbackSort(Span<string> span)
        {
            PDQ.Cmp<string, IComparer<string>>.Sort(span, Comparer);
        }

        [ImplTemplate("string", nameof(span))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void FallbackSortStable(Span<string> span, MemoryProfile profile)
        {
            if (span.Length < 32)
                IPN.Cmp<string, IComparer<string>>.SmallSort(span, Comparer);
            else
                Wiki.Cmp<string, IComparer<string>>.Sort(span, Comparer, profile);
        }

        [ImplTemplate("string", nameof(span), nameof(cache))]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void FallbackSortStableReuse(Span<string> span, Span<string> cache)
        {
            if (span.Length < 32)
                IPN.Cmp<string, IComparer<string>>.SmallSort(span, Comparer);
            else
            {
                var iterator = new Wiki.Iterator(span.Length, 4);
                Wiki.Cmp<string, IComparer<string>>.BlockSortLoop(span, Comparer, ref iterator);
                Wiki.Cmp<string, IComparer<string>>.SortLoop(span, cache, Comparer, ref iterator);
            }
        }

        /// <remarks>MSD-only</remarks>
        [StructLayout(LayoutKind.Auto)]
        private ref struct State() : IState
        {
            /// <remarks><see cref="string"/>.MaxLength is 0x3FFFFFDF.</remarks>
            public int Offset { get; private set; } = 0;
            public readonly int BucketCount => (1 << 8) + 1;
            public readonly bool TryMoveFromLsd() { ThrowHelper.ThrowUnreachable(); return false; }
            public bool TryMoveFromMsd() => ++Offset < int.MaxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Extract(ref readonly string value, ref readonly State state)
        {
            int c = state.Offset / 2;
            if ((uint)c >= (uint)value.Length)
                return 0;
#if NETCOREAPP3_0_OR_GREATER
            int offset = BitConverter.IsLittleEndian ? state.Offset ^ 1 : state.Offset;
            return Unsafe.RO.Add(in Unsafe.RO.As<char, byte>(in value.GetPinnableReference()), offset) + 1;
#else
            int shift = (state.Offset & 1) == 0 ? 8 : 0;
            return ((value[c] >>> shift) & 0xff) + 1;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool MsdSkipBucket(int b) => b == 0;

        [ImplTemplate("string", nameof(span))]
        [SkipLocalsInit]
        internal static void MsdSort(Span<string> span, MemoryProfile profile)
        {
            if (span.Length > FallbackSortThreshold)
            {
                var state = new State();
                Span<int> sums = stackalloc int[state.BucketCount + 1];
                if (profile < MemoryProfile.High) // unstable
                {
                    int start = SpanOperations.MoveNullsToFront(span);
                    MsdSortUnstable(span.Sub(start, span.Length), sums, state, profile);
                }
                else
                {
                    using MemoryOwner<string> owner = new(MemoryPool<string>.Shared.Rent(span.Length), span.Length);
                    Span<string> cache = owner.Memory.Span.Sub(0, span.Length);
                    int start = SpanOperations.MoveNullsToFrontStable(span, cache);
                    MsdSortBuffered(span.Sub(start, span.Length), cache.Sub(start, span.Length), sums, state, true);
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
                IKeySelector<V, string>
        {
            private static readonly KeySelectorComparer<V, string, TSelector, IComparer<string>> Comparer = new(Str.Comparer);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static int Compare(ref readonly V a, ref readonly V b) => Comparer.Compare(a, b);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static int Extract(ref readonly V value, ref readonly State state)
            {
#if NET7_0_OR_GREATER
                string key = TSelector.Select(in value)!;
#else
                string key = default(TSelector).SelectInst(in value)!;
#endif
                return Str.Extract(in key, in state);
            }
        }
    }
}
