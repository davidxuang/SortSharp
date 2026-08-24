using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SortSharp.Compat;

internal partial class BclPolyfills
{
    extension(Convert)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ToInt32<T>(T value)
            where T : unmanaged
#if NET7_0_OR_GREATER
            , IBinaryInteger<T>, IMinMaxValue<T>
#endif
            => value switch
            {
                byte u8 => u8,
                sbyte i8 => i8,
                short i16 => i16,
                ushort u16 => u16,
                int i32 => i32,
                uint u32 => (int)u32,
                long i64 => (int)i64,
                ulong u64 => (int)u64,
                nint isize => (int)isize,
                nuint usize => (int)usize,
#if NET7_0_OR_GREATER
                _ => int.CreateTruncating(value)
#else
                _ => Convert.ToInt32(value)
#endif
            };
    }
#if !NET7_0_OR_GREATER
    extension(BitConverter)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong DoubleToUInt64Bits(double d) => unchecked((ulong)BitConverter.DoubleToInt64Bits(d));
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int SingleToInt32Bits(float f) => Unsafe.As<float, int>(ref f);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint SingleToUInt32Bits(float f) => Unsafe.As<float, uint>(ref f);
#if NETSTANDARD2_0_COMPAT
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static short HalfToInt16Bits(Half h) => Unsafe.As<Half, short>(ref h);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort HalfToUInt16Bits(Half h) => Unsafe.As<Half, ushort>(ref h);
#endif
    }
#endif

#if !NET5_0_OR_GREATER
    extension(IntPtr)
    {
        internal static nint MinValue => IntPtr.Size switch
        {
            4 => int.MinValue,
            8 => unchecked((nint)long.MinValue),
            _ => throw new NotSupportedException()
        };
    }
#endif

#if NETSTANDARD && !NETSTANDARD2_0_OR_GREATER
    extension(Math)
    {
        internal static int DivRem(int a, int b, out int result)
        {
            result = a % b;
            return a / b;
        }
    }
#endif
}

#if !NETCOREAPP3_0_OR_GREATER
internal static class BitOperations
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Log2(uint value)
    {
        int log = 0;
        while ((value >>= 1) != 0) log++;
        return log;
    }
}
#endif
