namespace SortSharp.Testing;

public enum DoublePattern
{
    RandomFinite,
    RandomCategory,
    NanTail5,
}

public readonly struct DoubleGenerator()
{
    private readonly byte[] bytes = new byte[sizeof(double)];

    public double[] GetArray(int length, DoublePattern pattern)
    {
        var array = new double[length];
        var random = new Random(42);

        switch (pattern)
        {
            case DoublePattern.RandomFinite:
                for (int i = 0; i < length; i++)
                    array[i] = NextFinite(random);
                break;
            case DoublePattern.RandomCategory:
                for (int i = 0; i < length; i++)
                    array[i] = random.Next(-8, 8) switch
                    {
                        -6 => double.NegativeInfinity,
                        -1 => double.NegativeZero,
                        0 => 0d,
                        5 => double.PositiveInfinity,
                        -7 or -8 or 6 or 7 => NextNaN(random),
                        _ => NextFinite(random),
                    };
                break;
            case DoublePattern.NanTail5:
                int j = 0;
                for (; j < length * 19 / 20; j++)
                    array[j] = NextFinite(random);
                for (; j < length; j++)
                    array[j] = NextNaN(random);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(pattern));
        }

        return array;
    }

    private double NextFinite(Random random)
    {
        double value;
        do
        {
            random.NextBytes(bytes);
            value = BitConverter.ToDouble(bytes, 0);
        } while (!double.IsFinite(value));
        return value;
    }

    private const long ExponentMask = 0x7FF0_0000_0000_0000;
    private const long MantissaMask = 0x000F_FFFF_FFFF_FFFF;
    private const long SignMask     = unchecked((long)0x8000_0000_0000_0000);

    private double NextNaN(Random random)
    {
        long bits;
        do
        {
            random.NextBytes(bytes);
            bits = BitConverter.ToInt64(bytes, 0);
        } while ((bits & MantissaMask) == 0); // reject Inf
        bits = (bits & (SignMask | MantissaMask)) | ExponentMask;
        return BitConverter.Int64BitsToDouble(bits);
    }
}

public enum BfpIeeeOrder
{
    Default,
    TotalIeee754,
}
