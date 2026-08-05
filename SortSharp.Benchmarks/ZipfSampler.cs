using System;

namespace SortSharp.Benchmarks;

internal readonly struct ZipfSampler
{
    private readonly long _count;
    private readonly double _exponent;
    private readonly double _hIntegralX1;
    private readonly double _hIntegralCount;
    private readonly double _squeeze;

    public ZipfSampler(long count, double exponent)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        if (!(exponent > 0.0) || !double.IsFinite(exponent))
            throw new ArgumentOutOfRangeException(nameof(exponent));

        _count = count;
        _exponent = exponent;
        _hIntegralX1 = HIntegral(1.5, exponent) - 1.0;
        _hIntegralCount = HIntegral(count + 0.5, exponent);
        _squeeze = 2.0 - HIntegralInverse(
            HIntegral(2.5, exponent) - H(2.0, exponent),
            exponent);
    }

    public long Next(Random random)
    {
        while (true)
        {
            double u = _hIntegralX1
                + random.NextDouble()
                * (_hIntegralCount - _hIntegralX1);

            double x = HIntegralInverse(u, _exponent);
            long k = Math.Clamp((long)(x + 0.5), 1, _count);

            if (k - x <= _squeeze ||
                u >= HIntegral(k + 0.5, _exponent) -
                    H(k, _exponent))
            {
                return k - 1;
            }
        }
    }

    private static double H(double x, double exponent)
        => Math.Pow(x, -exponent);

    private static double HIntegral(double x, double exponent)
    {
        if (exponent == 1.0)
            return Math.Log(x);

        double oneMinusExponent = 1.0 - exponent;
        return (Math.Pow(x, oneMinusExponent) - 1.0)
            / oneMinusExponent;
    }

    private static double HIntegralInverse(double x, double exponent)
    {
        if (exponent == 1.0)
            return Math.Exp(x);

        double oneMinusExponent = 1.0 - exponent;
        return Math.Pow(
            1.0 + oneMinusExponent * x,
            1.0 / oneMinusExponent);
    }
}
