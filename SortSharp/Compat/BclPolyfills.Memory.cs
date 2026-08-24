using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SortSharp.Compat;

partial class BclPolyfills
{
#if !NET8_0_OR_GREATER
    extension(Unsafe)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullRef<T>([AllowNull] ref readonly T source)
            => Unsafe.IsNullRef(ref Unsafe.AsRef(in source));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nint ByteOffset<T>([AllowNull] ref readonly T origin, [AllowNull] ref readonly T target)
            => Unsafe.ByteOffset(ref Unsafe.AsRef(in origin), ref Unsafe.AsRef(in target));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreSame<T>([AllowNull] ref readonly T left, [AllowNull] ref readonly T right)
            => Unsafe.AreSame(ref Unsafe.AsRef(in left), ref Unsafe.AsRef(in right));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAddressLessThan<T>([AllowNull] ref readonly T left, [AllowNull] ref readonly T right)
            => Unsafe.IsAddressLessThan(ref Unsafe.AsRef(in left), ref Unsafe.AsRef(in right));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAddressGreaterThan<T>([AllowNull] ref readonly T left, [AllowNull] ref readonly T right)
            => Unsafe.IsAddressGreaterThan(ref Unsafe.AsRef(in left), ref Unsafe.AsRef(in right));
    }

    extension(MemoryMarshal)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySpan<T> CreateReadOnlySpan<T>(ref readonly T reference, int length)
            => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in reference), length);
    }
#endif
}
