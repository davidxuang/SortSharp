using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SortSharp.Compat;
using SortSharp.Foundation;
using SortSharp.SourceGeneration;
using static SortSharp.SpanOperations;
using Digit = int;

namespace SortSharp;

public static partial class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RadixMsdSort(this Span<string> span, MemoryProfile profile = MemoryProfile.Baseline)
    {
        Radix.Str.MsdSort(span, profile);
    }

    public static void RadixMsdSort<V>(this Span<string> keys, Span<V> items, MemoryProfile profile = MemoryProfile.Baseline)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(keys.Length, items.Length, nameof(items));
        Radix.Str.MsdSort(keys, items, profile);
    }
}

internal partial class Radix
{
    [SortSpecialization("string")]
    internal static partial class Str
    {
        static readonly StringComparer Comparer = StringComparer.Ordinal;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Compare(ref readonly string a, ref readonly string b) => Comparer.Compare(a, b);

        [OverloadTemplate("string", null, nameof(span), Disable = DefaultOverloads.SiblingSpecializations)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void FallbackSort(Span<string> span)
        {
            PDQ.Cmp<string, IComparer<string>>.Sort(span, Comparer);
        }

        [OverloadTemplate("string", null, nameof(span), Disable = DefaultOverloads.SiblingSpecializations)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void FallbackSortStable(Span<string> span, MemoryProfile profile)
        {
            if (span.Length < 32)
                IPN.Cmp<string, IComparer<string>>.SmallSort(span, Comparer);
            else
                Wiki.Cmp<string, IComparer<string>>.Sort(span, Comparer, profile);
        }

        [OverloadTemplate("string", null, nameof(span), nameof(cache), Disable = DefaultOverloads.SiblingSpecializations)]
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

        private ref struct LsdState
        {
            public readonly int BucketCount => throw new NotSupportedException();
            public readonly bool MoveNext() => throw new NotSupportedException();
            public readonly Digit Current => throw new NotSupportedException();
        }

        [StructLayout(LayoutKind.Auto)]
        private ref struct MsdState()
        {
            /// <remarks><see cref="string"/>.MaxLength is 0x3FFFFFDF.</remarks>
            public int Offset { get; private set; } = 0;
            public readonly int BucketCount => (1 << 8) + 1;
            public bool MoveNext() => ++Offset < int.MaxValue;
            public readonly Digit Current => Offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Extract(ref readonly string value, ref readonly Digit digit)
        {
            int c = digit / 2;
            if ((uint)c >= (uint)value.Length)
                return 0;
#if NETCOREAPP3_0_OR_GREATER
            int offset = BitConverter.IsLittleEndian ? digit ^ 1 : digit;
            return Unsafe.RO.Add(in Unsafe.RO.As<char, byte>(in value.GetPinnableReference()), offset) + 1;
#else
            int shift = (digit & 1) == 0 ? 8 : 0;
            return ((value[c] >>> shift) & 0xff) + 1;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool MsdSkipBucket(int b) => b == 0;

        [OverloadTemplate("string", null, nameof(span), Disable = DefaultOverloads.SiblingSpecializations)]
        [SkipLocalsInit]
        internal static void MsdSort(Span<string> span, MemoryProfile profile)
        {
            if (span.Length > FallbackSortThreshold)
            {
                if (profile < MemoryProfile.High) // unstable
                {
                    int start = MoveNullsToFront(span);
                    var state = new MsdState();
                    MsdSortUnstable(span.Sub(start, span.Length), stackalloc int[state.BucketCount + 1], state, profile);
                }
                else
                {
                    using MemoryOwner<string> owner = new(MemoryPool<string>.Shared.Rent(span.Length), span.Length);
                    Span<string> cache = owner.Memory.Span.Sub(0, span.Length);
                    int start = MoveNullsToFrontStable(span, cache);
                    var state = new MsdState();
                    MsdSortBuffered(span.Sub(start, span.Length), cache.Sub(start, span.Length), stackalloc int[state.BucketCount + 1], state, true);
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
    }
}
