using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SortSharp.Benchmarks;

public enum IntegerPattern
{
    Random,
    Sorted,
    Reverse,

    SortedHead95,
    Noisy,
    RandomRuns,

    OrganPipe,
    Stagger,
    Sawtooth61,

    AllZeros,
    AlmostZeros95,
    Random16,
    Zipf1,
}

internal static class IntegerGenerator<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public static T[] GetArray(int length, IntegerPattern pattern)
    {
        var array = new T[length];
        var random = new Random(42);

        switch (pattern)
        {
            case IntegerPattern.Random:
                for (int i = 0; i < length; i++)
                    array[i] = Next(random);
                break;
            case IntegerPattern.Sorted:
                for (int i = 0; i < length; i++)
                    array[i] = T.CreateChecked(i);
                break;
            case IntegerPattern.Reverse:
                for (int i = 0; i < length; i++)
                    array[i] = T.CreateChecked(length - i);
                break;
            case IntegerPattern.SortedHead95:
                for (int i = 0; i < length; i++)
                    array[i] = Next(random);
                array.AsSpan(0, length * 19 / 20).Sort();
                break;
            case IntegerPattern.RandomRuns:
                for (int i = 0; i < length; i++)
                    array[i] = T.CreateChecked(i);
                random.Shuffle(array);
                double prob = 1.0 / Math.Sqrt(length);
                for (int i = 0; i < length; )
                {
                    int run = NextGeometric(random, prob);
                    run = Math.Min(run, length - i);
                    array.AsSpan(i, run).Sort();
                    i += run;
                }
                break;
            case IntegerPattern.Noisy:
                int window = Math.Max(4, (int)Math.Sqrt(length) / 2);
                for (int i = 0; i < length; i++)
                    array[i] = T.CreateChecked(i);
                int s = 0;
                while (s + window <= length)
                {
                    random.Shuffle(array.AsSpan(s, window));
                    s += random.Next(1, window);
                }
                random.Shuffle(array[s..]);
                break;
            case IntegerPattern.OrganPipe:
                for (int i = 0; i < length / 2; i++)
                    array[i] = T.CreateChecked(i);
                for (int i = length / 2; i < length; i++)
                    array[i] = T.CreateChecked(length - i);
                break;
            case IntegerPattern.Stagger:
                int step = length / 2 + 1;
                for (int i = 0; i < length; i++)
                    array[i] = T.CreateChecked((long)i * step % length);
                break;
            case IntegerPattern.Sawtooth61:
                for (int i = 0; i < length; i++)
                    array[i] = T.CreateChecked(i % 61);
                break;
            case IntegerPattern.AllZeros:
                for (int i = 0; i < length; i++)
                    array[i] = T.Zero;
                break;
            case IntegerPattern.AlmostZeros95:
                for (int i = 0; i < length; i++)
                {
                    if (random.Next(20) == 0)
                        array[i] = Next(random);
                    else
                        array[i] = T.Zero;
                }
                break;
            case IntegerPattern.Random16:
                for (int i = 0; i < length; i++)
                    array[i] = T.CreateChecked(random.Next(16));
                break;
            case IntegerPattern.Zipf1:
                var zipf = new ZipfSampler(long.MaxValue, 1.0);
                for (int i = 0; i < length; i++)
                {
                    long value = zipf.Next(random);
                    array[i] = T.CreateChecked(value);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(pattern));
        }

        return array;
    }

    private static T Next(Random random)
    {
        Span<byte> bytes = stackalloc byte[Unsafe.SizeOf<T>()];
        random.NextBytes(bytes);
        return MemoryMarshal.Read<T>(bytes);
    }

    private static int NextGeometric(Random random, double probability)
    {
        // Geometric distribution with support [1, +∞).
        double u = random.NextDouble();

        return 1 + (int)(
            Math.Log(1.0 - u) /
            Math.Log(1.0 - probability));
    }

    // Rejection-inversion sampler specialized for Zipf(N, 1).
    private static long NextZipf1(
        Random random,
        long numberOfElements,
        double hIntegralX1,
        double hIntegralNumberOfElements,
        double squeeze)
    {
        while (true)
        {
            double u = hIntegralNumberOfElements
                + random.NextDouble() * (hIntegralX1 - hIntegralNumberOfElements);
            double x = Math.Exp(u);
            long k = Math.Clamp((long)(x + 0.5), 1, numberOfElements);

            if (k - x <= squeeze || u >= Math.Log(k + 0.5) - 1.0 / k)
                return k;
        }
    }
}
