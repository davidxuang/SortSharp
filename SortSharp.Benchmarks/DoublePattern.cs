using System;

namespace SortSharp.Benchmarks;

public enum DoublePattern
{
    RandomFinite,
    RandomCategory,
    NanTail5,
}

internal static class DoubleGenerator
{
    public static double[] GetArray(int length, DoublePattern pattern)
    {
        var array = new double[length];
        var random = new Random(42);

        switch (pattern)
        {
            case DoublePattern.RandomFinite:
                for (int i = 0; i < length; i++)
                    array[i] = random.NextFinite();
                break;
            case DoublePattern.RandomCategory:
                for (int i = 0; i < length; i++)
                    array[i] = random.Next(-8, 8) switch
                    {
                        -6 => double.NegativeInfinity,
                        -1 => double.NegativeZero,
                        0 => 0d,
                        5 => double.PositiveInfinity,
                        -7 or -8 or 6 or 7 => random.NextNaN(),
                        _ => random.NextFinite(),
                    };
                break;
            case DoublePattern.NanTail5:
                int j = 0;
                for (; j < length * 19 / 20; j++)
                    array[j] = random.NextFinite();
                for (; j < length; j++)
                    array[j] = random.NextNaN();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(pattern));
        }

        return array;
    }

    private static double NextFinite(this Random random)
    {
        Span<byte> bytes = stackalloc byte[sizeof(double)];
        double value;
        do
        {
            random.NextBytes(bytes);
            value = BitConverter.ToDouble(bytes);
        } while (!double.IsFinite(value));
        return value;
    }

    private const ulong ExponentMask = 0x7FF0_0000_0000_0000;
    private const ulong MantissaMask = 0x000F_FFFF_FFFF_FFFF;
    private const ulong SignMask     = 0x8000_0000_0000_0000;

    private static double NextNaN(this Random random)
    {
        Span<byte> bytes = stackalloc byte[sizeof(double)];
        ulong bits;
        do
        {
            random.NextBytes(bytes);
            bits = BitConverter.ToUInt64(bytes);
        } while ((bits & MantissaMask) == 0); // reject Inf
        bits = (bits & (SignMask | MantissaMask)) | ExponentMask;
        return BitConverter.UInt64BitsToDouble(bits);
    }
}
